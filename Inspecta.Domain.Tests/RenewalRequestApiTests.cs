using System.Net;
using System.Net.Http.Json;
using Inspecta.Application.Contracts.Renewals;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inspecta.Domain.Tests;

/// <summary>The last step of Renewals: the client answered (or was called), and the reminder becomes a normal request, once. Or it is closed without one.</summary>
public sealed class RenewalRequestApiTests(ApiHttpTests.Api api) : ExecutiveApiTestBase(api), IClassFixture<ApiHttpTests.Api>
{
    private Guid SeedClientWithEmail(World world, string name = "Delta Steel Co.")
    {
        var client = Client.Create(name, managerId: world.Company.Id, location: "10th of Ramadan", certificatesEmail: "certs@" + name.Split(' ')[0].ToLowerInvariant() + ".com");
        Api.SeedAs(db => db.Clients.Add(client));
        return client.Id;
    }

    /// <summary>A reminder of the coordinator for the client, as the send step leaves it: sent, failed or still sending.</summary>
    private Guid SeedReminder(World world, Guid clientId, Guid[] machines, OutboundEmailStatus status = OutboundEmailStatus.Sent)
    {
        var reminder = RenewalReminder.Create(world.Company.Id, clientId, world.Coordinator.Id, Guid.NewGuid(), "integrity-intl.com");
        reminder.BeginAttempt("certs@deltasteel.com", null, "hager@integrity-intl.com", "Hager Samy", "Periodic inspection due", "Dear client",
            machines.Select(id => new ReminderMachine(id, "Machine", null, null, Today.AddDays(10))));
        if (status == OutboundEmailStatus.Sent) reminder.MarkSent();
        if (status == OutboundEmailStatus.Failed) reminder.MarkFailed("refused");
        Api.SeedAs(db => db.RenewalReminders.Add(reminder));
        return reminder.Id;
    }

    private int RequestsOf(Guid clientId)
    {
        var count = 0;
        Api.SeedAs(db => count = db.Requests.IgnoreQueryFilters().Count(item => item.ClientId == clientId));
        return count;
    }

    [Fact]
    public async Task ARenewalRequestHasTheTickedMachinesAndThePreferredDateAndTheReminderPointsToIt()
    {
        var world = SeedCompany();
        var client = SeedClientWithEmail(world);
        var crane = SeedMachine(client, 10, name: "Overhead crane 10 t");
        var hoist = SeedMachine(client, -3, name: "Chain hoist 2 t");
        var reminder = SeedReminder(world, client, [crane, hoist]);
        var coordinator = await SignedInAs(world.Coordinator.Email);

        var response = await coordinator.PostAsJsonAsync("/api/v1/renewals/requests",
            new { clientId = client, reminderId = reminder, equipmentIds = new[] { crane, hoist }, preferredDate = "2026-12-02T00:00:00Z" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var json = await Json(response);
        Assert.Equal("request_received", json.GetProperty("status").GetString());
        Assert.Equal("manual", json.GetProperty("source").GetString());
        Assert.Equal(client, json.GetProperty("clientId").GetGuid());
        Assert.Equal("Delta Steel Co.", json.GetProperty("clientName").GetString());
        Assert.Equal("10th of Ramadan", json.GetProperty("location").GetString());
        Assert.StartsWith("Renewal: 2 machines", json.GetProperty("subject").GetString());
        Assert.Equal(world.Manager.Id, json.GetProperty("reviewerId").GetGuid());
        var items = json.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(new[] { crane, hoist }.Order().ToArray(), items.Select(item => item.GetProperty("equipmentId").GetGuid()).Order().ToArray());
        Assert.All(items, item => Assert.Equal(1, item.GetProperty("quantity").GetInt32()));
        Assert.Contains("2026-12-02", json.GetProperty("preferredDate").GetString());

        var stored = await Json(await coordinator.GetAsync($"/api/v1/renewals/reminders/{reminder}"));
        Assert.Equal("requested", stored.GetProperty("state").GetString());
        Assert.Equal(json.GetProperty("id").GetGuid(), stored.GetProperty("requestId").GetGuid());
        Assert.Equal(1, RequestsOf(client));
    }

    [Fact]
    public async Task TheErrorsNameTheMachinesAndTheRequestInProblems()
    {
        var world = SeedCompany();
        var client = SeedClientWithEmail(world);
        var scrapped = SeedMachine(client, 5, name: "Scrapped hoist");
        Api.SeedAs(db => db.Items.Single(item => item.Id == scrapped).Retire());
        var machine = SeedMachine(client, 6);
        var reminder = SeedReminder(world, client, [machine]);
        var coordinator = await SignedInAs(world.Coordinator.Email);

        var refused = await Json(await coordinator.PostAsJsonAsync("/api/v1/renewals/requests", new { clientId = client, equipmentIds = new[] { scrapped } }));
        Assert.Contains("Scrapped hoist: out of service", refused.GetProperty("problems").EnumerateArray().Select(item => item.GetString()));

        var created = await Json(await coordinator.PostAsJsonAsync("/api/v1/renewals/requests", new { clientId = client, reminderId = reminder, equipmentIds = new[] { machine } }));
        var again = await Json(await coordinator.PostAsJsonAsync("/api/v1/renewals/requests", new { clientId = client, reminderId = reminder, equipmentIds = new[] { machine } }));
        Assert.Equal(created.GetProperty("id").GetString(), again.GetProperty("problems")[0].GetString());
    }

    [Fact]
    public async Task AClaimThatIsLostAfterTheRequestWasMadeUndoesTheRequest()
    {
        var world = SeedCompany();
        var client = SeedClientWithEmail(world);
        var machine = SeedMachine(client, 6);
        var reminder = SeedReminder(world, client, [machine]);
        // Another click took the reminder between the check and the claim: the claim answers false.
        using var losing = Api.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddScoped<IRenewalsRepository>(provider => new LosesTheClaim(ActivatorUtilities.CreateInstance<EfRenewalsRepository>(provider)))));
        var coordinator = await SignedInAs(world.Coordinator.Email, losing);

        var response = await coordinator.PostAsJsonAsync("/api/v1/renewals/requests", new { clientId = client, reminderId = reminder, equipmentIds = new[] { machine } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("renewal_request_exists", (await Json(response)).GetProperty("code").GetString());
        Assert.Equal(0, RequestsOf(client));
    }

    private sealed class LosesTheClaim(IRenewalsRepository inner) : IRenewalsRepository
    {
        public Task<IReadOnlyList<RenewalClientRows>> ListDueAsync(Guid managerId, DateTimeOffset dueBefore, CancellationToken cancellationToken = default) => inner.ListDueAsync(managerId, dueBefore, cancellationToken);
        public Task<RenewalClientInfo?> GetClientAsync(Guid managerId, Guid clientId, CancellationToken cancellationToken = default) => inner.GetClientAsync(managerId, clientId, cancellationToken);
        public Task<IReadOnlyList<RenewalMachineInfo>> GetMachinesAsync(Guid managerId, IReadOnlyList<Guid> equipmentIds, CancellationToken cancellationToken = default) => inner.GetMachinesAsync(managerId, equipmentIds, cancellationToken);
        public Task<RenewalReminder?> GetReminderBySendKeyAsync(Guid managerId, Guid sendKey, CancellationToken cancellationToken = default) => inner.GetReminderBySendKeyAsync(managerId, sendKey, cancellationToken);
        public Task<RenewalReminder?> GetReminderAsync(Guid managerId, Guid reminderId, CancellationToken cancellationToken = default) => inner.GetReminderAsync(managerId, reminderId, cancellationToken);
        public Task<bool> TryAddReminderAsync(RenewalReminder reminder, CancellationToken cancellationToken = default) => inner.TryAddReminderAsync(reminder, cancellationToken);
        public Task SaveAsync(CancellationToken cancellationToken = default) => inner.SaveAsync(cancellationToken);
        public Task<bool> TrySaveAsync(CancellationToken cancellationToken = default) => inner.TrySaveAsync(cancellationToken);
        public Task ReloadReminderAsync(RenewalReminder reminder, CancellationToken cancellationToken = default) => inner.ReloadReminderAsync(reminder, cancellationToken);
        public Task<bool> ClaimReminderAsync(Guid managerId, Guid reminderId, Guid requestId, Guid actorId, DateTimeOffset at, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> CloseReminderAsync(Guid managerId, Guid reminderId, Guid actorId, DateTimeOffset at, CancellationToken cancellationToken = default) => inner.CloseReminderAsync(managerId, reminderId, actorId, at, cancellationToken);
        public Task<(IReadOnlyList<RenewalReminder> Items, int Total)> ListRemindersAsync(Guid managerId, Guid? clientId, int page, int pageSize, CancellationToken cancellationToken = default) => inner.ListRemindersAsync(managerId, clientId, page, pageSize, cancellationToken);
        public Task<IReadOnlyDictionary<Guid, RenewalReminder>> LatestByClientAsync(Guid managerId, IReadOnlyCollection<Guid> clientIds, CancellationToken cancellationToken = default) => inner.LatestByClientAsync(managerId, clientIds, cancellationToken);
        public Task<IReadOnlyList<RenewalReminderReply>> RepliesAsync(IReadOnlyCollection<Guid> reminderIds, CancellationToken cancellationToken = default) => inner.RepliesAsync(reminderIds, cancellationToken);
        public Task<RenewalReminder?> FindReminderByMessageIdAsync(Guid managerId, IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default) => inner.FindReminderByMessageIdAsync(managerId, messageIds, cancellationToken);
        public Task<bool> IsOwnReminderMessageAsync(Guid managerId, string messageId, CancellationToken cancellationToken = default) => inner.IsOwnReminderMessageAsync(managerId, messageId, cancellationToken);
        public Task<bool> ReplyExistsAsync(Guid managerId, string messageId, CancellationToken cancellationToken = default) => inner.ReplyExistsAsync(managerId, messageId, cancellationToken);
        public Task AddReplyAsync(RenewalReminderReply reply, RenewalReminder reminder, CancellationToken cancellationToken = default) => inner.AddReplyAsync(reply, reminder, cancellationToken);
    }

    [Fact]
    public async Task AClientWithRemindersCannotBeDeletedForGoodAndSaysSo()
    {
        var world = SeedCompany();
        var client = SeedClientWithEmail(world);
        SeedReminder(world, client, [Guid.NewGuid()]);
        var manager = await SignedInAs(world.Manager.Email);

        var response = await manager.DeleteAsync($"/api/v1/clients/{client}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("client_in_use", (await Json(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ARenewalRequestWorksWithoutAReminderAndWithoutAReply()
    {
        var world = SeedCompany();
        var client = SeedClientWithEmail(world);
        var machine = SeedMachine(client, 12);
        var coordinator = await SignedInAs(world.Coordinator.Email);

        var response = await coordinator.PostAsJsonAsync("/api/v1/renewals/requests", new { clientId = client, equipmentIds = new[] { machine } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.StartsWith("Renewal: 1 machine", (await Json(response)).GetProperty("subject").GetString());
        Assert.Equal(1, RequestsOf(client));
    }

    [Fact]
    public async Task ASecondRequestFromTheSameReminderIsRefusedAndExactlyOneRequestExists()
    {
        var world = SeedCompany();
        var client = SeedClientWithEmail(world);
        var machine = SeedMachine(client, 12);
        var reminder = SeedReminder(world, client, [machine]);
        var coordinator = await SignedInAs(world.Coordinator.Email);
        var body = new { clientId = client, reminderId = reminder, equipmentIds = new[] { machine } };

        Assert.Equal(HttpStatusCode.Created, (await coordinator.PostAsJsonAsync("/api/v1/renewals/requests", body)).StatusCode);
        var second = await coordinator.PostAsJsonAsync("/api/v1/renewals/requests", body);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("renewal_request_exists", (await Json(second)).GetProperty("code").GetString());
        Assert.Equal(1, RequestsOf(client));
    }

    [Fact]
    public async Task AMachineThatIsOutOfServiceOrOfAnotherClientCannotGoOnTheRequest()
    {
        var world = SeedCompany();
        var client = SeedClientWithEmail(world);
        var other = SeedClientWithEmail(world, "Suez Port Services");
        var scrapped = SeedMachine(client, 5);
        Api.SeedAs(db => db.Items.Single(item => item.Id == scrapped).Retire());
        var theirs = SeedMachine(other, 5);
        var coordinator = await SignedInAs(world.Coordinator.Email);

        foreach (var machine in new[] { scrapped, theirs })
        {
            var response = await coordinator.PostAsJsonAsync("/api/v1/renewals/requests", new { clientId = client, equipmentIds = new[] { machine } });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("machine_not_eligible", (await Json(response)).GetProperty("code").GetString());
        }
        var none = await coordinator.PostAsJsonAsync("/api/v1/renewals/requests", new { clientId = client, equipmentIds = Array.Empty<Guid>() });
        Assert.Equal("machines_required", (await Json(none)).GetProperty("code").GetString());
        Assert.Equal(0, RequestsOf(client));
    }

    [Fact]
    public async Task AReminderOfAnotherClientOrNotSentOrClosedCannotBeUsed()
    {
        var world = SeedCompany();
        var client = SeedClientWithEmail(world);
        var other = SeedClientWithEmail(world, "Suez Port Services");
        var machine = SeedMachine(client, 5);
        var ofOther = SeedReminder(world, other, [SeedMachine(other, 5)]);
        var failed = SeedReminder(world, client, [machine], OutboundEmailStatus.Failed);
        var closed = SeedReminder(world, client, [machine]);
        var coordinator = await SignedInAs(world.Coordinator.Email);
        Assert.Equal(HttpStatusCode.OK, (await coordinator.PostAsync($"/api/v1/renewals/reminders/{closed}/close", null)).StatusCode);

        async Task<(HttpStatusCode Status, string Code)> Try(Guid reminder)
        {
            var response = await coordinator.PostAsJsonAsync("/api/v1/renewals/requests", new { clientId = client, reminderId = reminder, equipmentIds = new[] { machine } });
            return (response.StatusCode, (await Json(response)).GetProperty("code").GetString()!);
        }

        Assert.Equal((HttpStatusCode.BadRequest, "reminder_of_other_client"), await Try(ofOther));
        Assert.Equal((HttpStatusCode.Conflict, "reminder_not_sent"), await Try(failed));
        Assert.Equal((HttpStatusCode.Conflict, "reminder_closed"), await Try(closed));
        Assert.Equal((HttpStatusCode.NotFound, "not_found"), await Try(Guid.NewGuid()));
        Assert.Equal(0, RequestsOf(client));
    }

    [Fact]
    public async Task ClosingAReminderEndsItWithoutARequestAndOnceItHasARequestItCannotBeClosed()
    {
        var world = SeedCompany();
        var client = SeedClientWithEmail(world);
        var machine = SeedMachine(client, 5);
        var declined = SeedReminder(world, client, [machine]);
        var asked = SeedReminder(world, client, [machine]);
        var coordinator = await SignedInAs(world.Coordinator.Email);

        var closed = await coordinator.PostAsync($"/api/v1/renewals/reminders/{declined}/close", null);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var json = await Json(closed);
        Assert.Equal("closed", json.GetProperty("state").GetString());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, json.GetProperty("closedAt").ValueKind);
        // Closing again changes nothing.
        Assert.Equal(HttpStatusCode.OK, (await coordinator.PostAsync($"/api/v1/renewals/reminders/{declined}/close", null)).StatusCode);

        await coordinator.PostAsJsonAsync("/api/v1/renewals/requests", new { clientId = client, reminderId = asked, equipmentIds = new[] { machine } });
        var tooLate = await coordinator.PostAsync($"/api/v1/renewals/reminders/{asked}/close", null);
        Assert.Equal(HttpStatusCode.Conflict, tooLate.StatusCode);
        Assert.Equal("renewal_request_exists", (await Json(tooLate)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task AnotherCompanysReminderIsNotFoundAndAnInspectorIsRefused()
    {
        var mine = SeedCompany();
        var theirs = SeedCompany();
        var theirClient = SeedClientWithEmail(theirs);
        var theirReminder = SeedReminder(theirs, theirClient, [SeedMachine(theirClient, 5)]);
        var coordinator = await SignedInAs(mine.Coordinator.Email);

        Assert.Equal(HttpStatusCode.NotFound, (await coordinator.PostAsync($"/api/v1/renewals/reminders/{theirReminder}/close", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coordinator.GetAsync($"/api/v1/renewals/reminders/{theirReminder}")).StatusCode);

        var inspector = await SignedInAs(mine.Inspector.Email);
        Assert.Equal(HttpStatusCode.Forbidden, (await inspector.PostAsJsonAsync("/api/v1/renewals/requests", new { clientId = theirClient, equipmentIds = new[] { Guid.NewGuid() } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await inspector.PostAsync($"/api/v1/renewals/reminders/{theirReminder}/close", null)).StatusCode);
    }
}
