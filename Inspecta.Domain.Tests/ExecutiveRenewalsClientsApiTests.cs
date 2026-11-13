using System.Net;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The Renewals and Clients screens over HTTP.</summary>
public sealed class ExecutiveRenewalsClientsApiTests(ApiHttpTests.Api api) : ExecutiveApiTestBase(api), IClassFixture<ApiHttpTests.Api>
{
    private static int[] Buckets(JsonElement renewals)
    {
        var buckets = renewals.GetProperty("buckets");
        return new[] { "overdue", "within7", "within30", "within60", "within90" }.Select(name => buckets.GetProperty(name).GetInt32()).ToArray();
    }

    [Fact]
    public async Task Machines_land_in_the_right_buckets_and_archived_or_undated_machines_count_nowhere()
    {
        var world = SeedCompany();
        var client = SeedClient(world, "Steel");
        SeedMachine(client, 5);
        SeedMachine(client, -1, CertificateResult.SafeWithConditions);
        SeedMachine(client, 45);
        SeedMachine(client, 3, archived: true);
        SeedMachine(client, null);
        var gm = await SignedInAs(world.General.Email);

        var renewals = await Json(await gm.GetAsync("/api/v1/executive/renewals"));
        var soon = await Json(await gm.GetAsync("/api/v1/executive/renewals?filter=soon"));
        var overdue = await Json(await gm.GetAsync("/api/v1/executive/renewals?filter=overdue"));

        Assert.Equal([1, 1, 1, 2, 2], Buckets(renewals));
        Assert.Equal(3, renewals.GetProperty("registered").GetInt32());
        Assert.Equal(12, renewals.GetProperty("weekly").GetArrayLength());
        Assert.Equal(1, renewals.GetProperty("weekly")[0].GetInt32());
        Assert.Equal(1, soon.GetProperty("total").GetInt32());
        Assert.Equal(5, soon.GetProperty("items")[0].GetProperty("daysUntilDue").GetInt32());
        Assert.Equal(1, overdue.GetProperty("total").GetInt32());
        Assert.Equal("safe_with_conditions", overdue.GetProperty("items")[0].GetProperty("lastResult").GetString());
        Assert.Equal("Steel", overdue.GetProperty("mostOverdueByClient")[0].GetProperty("clientName").GetString());
        Assert.Equal(67, renewals.GetProperty("safePercent").GetInt32());
    }

    [Fact]
    public async Task Renewals_are_paged_and_a_wrong_filter_is_a_400()
    {
        var world = SeedCompany();
        var client = SeedClient(world, "Steel");
        foreach (var days in new[] { 1, 2, 3 }) SeedMachine(client, days);
        var gm = await SignedInAs(world.General.Email);

        var second = await Json(await gm.GetAsync("/api/v1/executive/renewals?pageSize=2&page=2"));
        var wrong = await gm.GetAsync("/api/v1/executive/renewals?filter=sideways");

        Assert.Equal(3, second.GetProperty("total").GetInt32());
        Assert.Equal(1, second.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
    }

    [Fact]
    public async Task A_machine_with_an_open_request_item_is_marked_as_having_a_job()
    {
        var world = SeedCompany();
        var client = SeedClient(world, "Steel");
        var machine = SeedMachine(client, 4);
        SeedMachine(client, 5);
        var request = InspectionRequest.Create("Steel", "Site", client, [RequestItem.Create("Crane", 1, equipmentId: machine)], world.Coordinator.Id, managerId: world.Company.Id);
        Api.SeedAs(db => db.Requests.Add(request));
        var gm = await SignedInAs(world.General.Email);

        var renewals = await Json(await gm.GetAsync("/api/v1/executive/renewals"));

        Assert.Equal(1, renewals.GetProperty("withJobCount").GetInt32());
        var items = renewals.GetProperty("items").EnumerateArray().ToArray();
        Assert.True(items.Single(item => item.GetProperty("equipmentId").GetGuid() == machine).GetProperty("jobOpened").GetBoolean());
        Assert.Equal(1, items.Count(item => item.GetProperty("jobOpened").GetBoolean()));
    }

    [Fact]
    public async Task Client_health_follows_the_rule_and_counts_ignore_the_filters()
    {
        var world = SeedCompany();
        var risky = SeedClient(world, "Risky Co");
        var late = SeedClient(world, "Late Co");
        var fine = SeedClient(world, "Fine Co");
        SeedMachine(risky, -4);
        SeedMachine(late, -2);
        SeedMachine(fine, 20);
        SeedInvoice(world, 500_000, ageDays: 70, clientId: risky, clientName: "Risky Co");
        var gm = await SignedInAs(world.General.Email);

        var all = await Json(await gm.GetAsync("/api/v1/executive/clients"));
        var watch = await Json(await gm.GetAsync("/api/v1/executive/clients?health=watch"));
        var search = await Json(await gm.GetAsync("/api/v1/executive/clients?q=fine"));

        var byName = all.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("name").GetString()!);
        Assert.Equal("at_risk", byName["Risky Co"].GetProperty("health").GetString());
        Assert.Equal("watch", byName["Late Co"].GetProperty("health").GetString());
        Assert.Equal("healthy", byName["Fine Co"].GetProperty("health").GetString());
        Assert.Equal(500_000, byName["Risky Co"].GetProperty("olderThan60Piastres").GetInt64());
        Assert.Equal(1, byName["Risky Co"].GetProperty("overdueMachines").GetInt32());
        Assert.Equal(["Late Co"], watch.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
        Assert.Equal(["Fine Co"], search.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
        Assert.Equal((3, 1, 1, 1), (watch.GetProperty("counts").GetProperty("active").GetInt32(), watch.GetProperty("counts").GetProperty("healthy").GetInt32(),
            watch.GetProperty("counts").GetProperty("watch").GetInt32(), watch.GetProperty("counts").GetProperty("atRisk").GetInt32()));
        Assert.Equal(HttpStatusCode.BadRequest, (await gm.GetAsync("/api/v1/executive/clients?health=fine")).StatusCode);
    }

    [Fact]
    public async Task The_client_page_has_contacts_machines_due_within_90_days_and_its_invoices()
    {
        var world = SeedCompany();
        var clientId = SeedClient(world, "Detail Co");
        SeedMachine(clientId, -10);
        SeedMachine(clientId, 40);
        SeedMachine(clientId, 200);
        SeedInvoice(world, 300_000, ageDays: 20, clientId: clientId, clientName: "Detail Co");
        Api.SeedAs(db =>
        {
            var client = db.Clients.Single(item => item.Id == clientId);
            client.ReplaceContacts([ClientContact.Create(client.Id, "Safety manager", "safety@detail.example", "0100", true)]);
        });
        var gm = await SignedInAs(world.General.Email);

        var detail = await Json(await gm.GetAsync($"/api/v1/executive/clients/{clientId}"));

        Assert.Equal("Detail Co", detail.GetProperty("name").GetString());
        Assert.Equal("Safety manager", detail.GetProperty("contacts")[0].GetProperty("name").GetString());
        Assert.Equal(2, detail.GetProperty("machines90").GetArrayLength());
        Assert.Equal(1, detail.GetProperty("invoices").GetArrayLength());
        Assert.Equal(300_000, detail.GetProperty("revenue").GetProperty("owedPiastres").GetInt64());
    }

    [Fact]
    public async Task Another_companys_client_is_a_404_and_its_machines_never_show()
    {
        var first = SeedCompany();
        var second = SeedCompany();
        var theirClient = SeedClient(second, "Their Client");
        SeedMachine(theirClient, 3);
        var gm = await SignedInAs(first.General.Email);

        var asked = await gm.GetAsync($"/api/v1/executive/clients/{theirClient}");
        var renewals = await Json(await gm.GetAsync("/api/v1/executive/renewals"));
        var clients = await Json(await gm.GetAsync("/api/v1/executive/clients"));

        Assert.Equal(HttpStatusCode.NotFound, asked.StatusCode);
        Assert.Equal(0, renewals.GetProperty("registered").GetInt32());
        Assert.Equal(0, clients.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await gm.GetAsync($"/api/v1/executive/clients/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task The_reviewer_filter_does_not_change_renewals_or_clients_and_only_the_gm_and_owner_may_ask()
    {
        var world = SeedCompany();
        var client = SeedClient(world, "Steel");
        SeedMachine(client, 5);
        var gm = await SignedInAs(world.General.Email);

        var plain = await Json(await gm.GetAsync("/api/v1/executive/renewals"));
        var filtered = await Json(await gm.GetAsync($"/api/v1/executive/renewals?reviewerId={world.Manager.Id}"));

        Assert.Equal(plain.GetProperty("registered").GetInt32(), filtered.GetProperty("registered").GetInt32());
        foreach (var email in new[] { world.Coordinator.Email, world.Finance.Email, world.CompanyAdmin.Email })
        {
            var user = await SignedInAs(email);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/executive/renewals")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/executive/clients")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"/api/v1/executive/clients/{client}")).StatusCode);
        }
    }
}
