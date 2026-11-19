using System.Net;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The Renewals list over HTTP: the machines that are due, grouped by client, for the coordinator to remind.</summary>
public sealed class RenewalsListApiTests(ApiHttpTests.Api api) : ExecutiveApiTestBase(api), IClassFixture<ApiHttpTests.Api>
{
    private Guid SeedClientWithContacts(World world, string name, string? certificatesEmail, params (string Name, string? Email)[] contacts)
    {
        var client = Client.Create(name, managerId: world.Company.Id, location: "10th of Ramadan", certificatesEmail: certificatesEmail);
        Api.SeedAs(db =>
        {
            db.Clients.Add(client);
            foreach (var contact in contacts) db.ClientContacts.Add(ClientContact.Create(client.Id, contact.Name, contact.Email, role: "Maintenance manager"));
        });
        return client.Id;
    }

    private void Retire(Guid machineId) => Api.SeedAs(db => db.Items.Single(item => item.Id == machineId).Retire());

    [Fact]
    public async Task TheListGroupsTheMachinesThatAreDueByClientAndLeavesOutRetiredArchivedAndUndatedOnes()
    {
        var world = SeedCompany();
        var delta = SeedClientWithContacts(world, "Delta Steel Co.", "certs@deltasteel.com", ("Ahmed Fawzy", "ahmed.fawzy@deltasteel.com"), ("No Email", null));
        var late = SeedMachine(delta, -6, name: "Chain hoist 2 t");
        var soon = SeedMachine(delta, 13, name: "Overhead crane 10 t");
        SeedMachine(delta, 45, name: "Man lift 12 m");
        var scrapped = SeedMachine(delta, 5, name: "Forklift 3 t");
        Retire(scrapped);
        SeedMachine(delta, 7, archived: true, name: "Archived crane");
        SeedMachine(delta, null, name: "Machine without a date");
        SeedMachine(delta, 400, name: "Far away");
        var coordinator = await SignedInAs(world.Coordinator.Email);

        var response = await coordinator.GetAsync("/api/v1/renewals?window=30");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await Json(response);
        var client = Assert.Single(json.GetProperty("clients").EnumerateArray());
        Assert.Equal("Delta Steel Co.", client.GetProperty("clientName").GetString());
        // Every active machine due within 90 days (or late) is listed for the drawer; the window says which are pre-ticked.
        var machines = client.GetProperty("machines").EnumerateArray().ToList();
        Assert.Equal(["Chain hoist 2 t", "Overhead crane 10 t", "Man lift 12 m"], machines.Select(item => item.GetProperty("name").GetString()).ToArray());
        Assert.Equal([true, true, false], machines.Select(item => item.GetProperty("inWindow").GetBoolean()).ToArray());
        Assert.Equal(late, machines[0].GetProperty("equipmentId").GetGuid());
        Assert.Equal(-6, machines[0].GetProperty("daysUntilDue").GetInt32());
        Assert.Equal(13, machines[1].GetProperty("daysUntilDue").GetInt32());
        Assert.Equal(1, client.GetProperty("lateInWindow").GetInt32());
        Assert.Equal(2, client.GetProperty("dueInWindow").GetInt32());
        Assert.DoesNotContain(machines, item => item.GetProperty("equipmentId").GetGuid() == scrapped);
        Assert.NotEqual(soon, Guid.Empty);
        // Only addresses that exist are offered, the certificates email first.
        var recipients = client.GetProperty("recipients").EnumerateArray().ToList();
        Assert.Equal(["certs@deltasteel.com", "ahmed.fawzy@deltasteel.com"], recipients.Select(item => item.GetProperty("email").GetString()).ToArray());
        Assert.Equal("certificates_email", recipients[0].GetProperty("kind").GetString());
        Assert.Equal("contact", recipients[1].GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, client.GetProperty("latestReminder").ValueKind);
    }

    [Fact]
    public async Task TheCountsOfTheWindowsIncludeTheOverdueMachinesAndTheWindowsFilterTheClients()
    {
        var world = SeedCompany();
        var one = SeedClientWithContacts(world, "Al Noor Industries", "qhse@alnoor-ind.com");
        var two = SeedClientWithContacts(world, "Suez Port Services", "equipment@suezport.com");
        SeedMachine(one, -10);
        SeedMachine(one, 20);
        SeedMachine(two, 50);
        SeedMachine(two, 85);
        var coordinator = await SignedInAs(world.Coordinator.Email);

        var counts = (await Json(await coordinator.GetAsync("/api/v1/renewals?window=30"))).GetProperty("counts");
        Assert.Equal(1, counts.GetProperty("overdue").GetInt32());
        Assert.Equal(2, counts.GetProperty("within30").GetInt32());
        Assert.Equal(3, counts.GetProperty("within60").GetInt32());
        Assert.Equal(4, counts.GetProperty("within90").GetInt32());

        var overdue = await Json(await coordinator.GetAsync("/api/v1/renewals?window=overdue"));
        Assert.Equal("Al Noor Industries", Assert.Single(overdue.GetProperty("clients").EnumerateArray()).GetProperty("clientName").GetString());
        var sixty = await Json(await coordinator.GetAsync("/api/v1/renewals?window=60"));
        Assert.Equal(["Al Noor Industries", "Suez Port Services"], sixty.GetProperty("clients").EnumerateArray().Select(item => item.GetProperty("clientName").GetString()).ToArray());
        Assert.Equal(2, sixty.GetProperty("total").GetInt32());
        var ninety = await Json(await coordinator.GetAsync("/api/v1/renewals?window=90"));
        Assert.Equal(2, ninety.GetProperty("clients").EnumerateArray().Last().GetProperty("dueInWindow").GetInt32());
    }

    [Fact]
    public async Task AMachineOnAnOpenRequestIsMarkedAndAMachineOnAClosedOneIsNot()
    {
        var world = SeedCompany();
        var client = SeedClientWithContacts(world, "Delta Steel Co.", "certs@deltasteel.com");
        var onRequest = SeedMachine(client, 10, name: "On a request");
        var onDelivered = SeedMachine(client, 11, name: "Already delivered");
        var free = SeedMachine(client, 12, name: "Free");
        var open = InspectionRequest.Create("Delta Steel Co.", "Site", client, [RequestItem.Create("Crane", 1)], world.Coordinator.Id, managerId: world.Company.Id);
        var finished = InspectionRequest.Create("Delta Steel Co.", "Site", client, [RequestItem.Create("Crane", 1)], world.Coordinator.Id, managerId: world.Company.Id);
        finished.TransitionTo(JobStatus.QuoteDraft, world.Coordinator.Id);
        finished.TransitionTo(JobStatus.QuoteSent, world.Coordinator.Id);
        finished.TransitionTo(JobStatus.QuoteRejected, world.Coordinator.Id, "The client chose another company");
        Api.SeedAs(db =>
        {
            db.Requests.Add(open);
            db.Requests.Add(finished);
            db.SaveChanges();
            db.RequestItems.Single(item => item.RequestId == open.Id).MatchEquipment(onRequest);
            db.RequestItems.Single(item => item.RequestId == finished.Id).MatchEquipment(onDelivered);
            db.SaveChanges();
        });
        var coordinator = await SignedInAs(world.Coordinator.Email);

        var machines = (await Json(await coordinator.GetAsync("/api/v1/renewals?window=30"))).GetProperty("clients")[0].GetProperty("machines").EnumerateArray().ToList();

        bool OnRequest(Guid id) => machines.Single(item => item.GetProperty("equipmentId").GetGuid() == id).GetProperty("onOpenRequest").GetBoolean();
        Assert.True(OnRequest(onRequest));
        Assert.False(OnRequest(onDelivered));
        Assert.False(OnRequest(free));
    }

    [Fact]
    public async Task OnlyTheCallersCompanyIsShownAndThePlatformOwnerMustNameOne()
    {
        var mine = SeedCompany();
        var other = SeedCompany();
        var myClient = SeedClientWithContacts(mine, "My Client", "mine@example.com");
        var otherClient = SeedClientWithContacts(other, "Their Client", "theirs@example.com");
        SeedMachine(myClient, 5);
        SeedMachine(otherClient, 5);

        var coordinator = await SignedInAs(mine.Coordinator.Email);
        var names = (await Json(await coordinator.GetAsync("/api/v1/renewals"))).GetProperty("clients").EnumerateArray().Select(item => item.GetProperty("clientName").GetString()).ToArray();
        Assert.Equal(["My Client"], names);

        var owner = await SignedInAs(mine.Owner.Email);
        var missing = await owner.GetAsync("/api/v1/renewals");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("manager_required", (await Json(missing)).GetProperty("code").GetString());
        var named = await Json(await owner.GetAsync($"/api/v1/renewals?managerId={other.Company.Id}"));
        Assert.Equal(["Their Client"], named.GetProperty("clients").EnumerateArray().Select(item => item.GetProperty("clientName").GetString()).ToArray());
    }

    [Fact]
    public async Task OnlyTheRolesThatWorkOnRequestsCanOpenTheList()
    {
        var world = SeedCompany();

        Assert.Equal(HttpStatusCode.OK, (await (await SignedInAs(world.Coordinator.Email)).GetAsync("/api/v1/renewals")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await SignedInAs(world.Manager.Email)).GetAsync("/api/v1/renewals")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await SignedInAs(world.Inspector.Email)).GetAsync("/api/v1/renewals")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await SignedInAs(world.CompanyAdmin.Email)).GetAsync("/api/v1/renewals")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await SignedInAs(world.General.Email)).GetAsync("/api/v1/renewals")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await SignedInAs(world.Finance.Email)).GetAsync("/api/v1/renewals")).StatusCode);
    }

    [Fact]
    public async Task TheListHasTheSameMachinesAsTheExecutiveRenewals()
    {
        var world = SeedCompany();
        var client = SeedClientWithContacts(world, "Delta Steel Co.", "certs@deltasteel.com");
        SeedMachine(client, -3);
        SeedMachine(client, 20);
        SeedMachine(client, 60);
        var retired = SeedMachine(client, 8);
        Retire(retired);
        SeedMachine(client, 8, archived: true);

        var executive = await Json(await (await SignedInAs(world.General.Email)).GetAsync("/api/v1/executive/renewals?filter=all&pageSize=100"));
        var executiveIds = executive.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("equipmentId").GetGuid()).Order().ToArray();
        var listed = await Json(await (await SignedInAs(world.Coordinator.Email)).GetAsync("/api/v1/renewals?window=90"));
        var listedIds = listed.GetProperty("clients")[0].GetProperty("machines").EnumerateArray().Select(item => item.GetProperty("equipmentId").GetGuid()).Order().ToArray();

        Assert.Equal(executiveIds, listedIds);
    }

    [Fact]
    public async Task ListingChangesNothingAndSendsNothing()
    {
        var world = SeedCompany();
        var client = SeedClientWithContacts(world, "Delta Steel Co.", "certs@deltasteel.com");
        var machine = SeedMachine(client, 4);
        var coordinator = await SignedInAs(world.Coordinator.Email);

        await coordinator.GetAsync("/api/v1/renewals?window=30");

        Api.SeedAs(db =>
        {
            var item = db.Items.Single(row => row.Id == machine);
            Assert.Equal(EquipmentStatus.Active, item.Status);
            Assert.Null(item.RetiredAt);
        });
    }
}
