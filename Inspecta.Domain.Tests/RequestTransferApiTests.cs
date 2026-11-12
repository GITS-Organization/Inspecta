using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>Handing a request to another coordinator over HTTP: who may, to whom, until when, and what each side sees afterwards.</summary>
public sealed class RequestTransferApiTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 150;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public RequestTransferApiTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.90.20." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    private AppUser Coordinator(Guid? team = null) =>
        api.AddUser($"transfer-coordinator-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Coordinator, team ?? api.ManagerA);

    /// <summary>An email request that arrived in the owner's mailbox, so only the owner (and the managers) can see it.</summary>
    private InspectionRequest SeedEmailRequest(AppUser owner, Guid? team = null)
    {
        var request = InspectionRequest.CreateFromEmail(
            team ?? api.ManagerA, owner.Id,
            new IncomingEmail("1", Interlocked.Increment(ref sequence), "Please quote a crane", null, "buyer@alnoor.com", "Al Noor", "Crane", [], [], [], [], [],
                $"<{Guid.NewGuid()}@alnoor.com>", null, null, DateTimeOffset.UtcNow),
            clientName: "Al Noor", location: "Cairo");
        api.SeedAs(db => db.Requests.Add(request));
        return request;
    }

    private static Task<HttpResponseMessage> Transfer(HttpClient client, Guid requestId, Guid to, string? note = null) =>
        client.PostAsJsonAsync($"/api/v1/requests/{requestId}/transfer", new { coordinatorId = to, note });

    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    [Fact]
    public async Task TheOwnerHandsAnEmailRequestOverAndEachSideSeesTheRightThing()
    {
        var first = Coordinator();
        var second = Coordinator();
        var request = SeedEmailRequest(first);
        var one = await SignedInAs(first.Email);
        var two = await SignedInAs(second.Email);
        Assert.Equal(HttpStatusCode.NotFound, (await two.GetAsync($"/api/v1/requests/{request.Id}")).StatusCode);

        var response = await Transfer(one, request.Id, second.Id, "Hassan handles cranes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(second.Id, json.RootElement.GetProperty("coordinatorId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await one.GetAsync($"/api/v1/requests/{request.Id}")).StatusCode);
        var seen = await two.GetAsync($"/api/v1/requests/{request.Id}");
        Assert.Equal(HttpStatusCode.OK, seen.StatusCode);
        var body = await seen.Content.ReadAsStringAsync();
        Assert.Contains("Transferred from", body);
        Assert.Contains("Hassan handles cranes", body);
    }

    [Fact]
    public async Task AnotherCoordinatorOfTheTeamCannotTransferARequestThatIsNotTheirs()
    {
        var owner = Coordinator();
        var other = Coordinator();
        var target = Coordinator();
        var manual = InspectionRequest.Create("Manual client", "Cairo", null, [RequestItem.Create("Crane", 1)], owner.Id, managerId: api.ManagerA);
        api.SeedAs(db => db.Requests.Add(manual));
        var email = SeedEmailRequest(owner);
        var client = await SignedInAs(other.Email);

        Assert.Equal(HttpStatusCode.Forbidden, (await Transfer(client, manual.Id, target.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Transfer(client, email.Id, target.Id)).StatusCode);
    }

    [Fact]
    public async Task AnInspectorIsRefused()
    {
        var owner = Coordinator();
        var target = Coordinator();
        var request = SeedEmailRequest(owner);
        var inspector = api.AddUser($"transfer-inspector-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Inspector, api.ManagerA);
        var client = await SignedInAs(inspector.Email);

        Assert.Equal(HttpStatusCode.Forbidden, (await Transfer(client, request.Id, target.Id)).StatusCode);
    }

    [Fact]
    public async Task AfterAQuotationWasSentTheCoordinatorCannotButTheManagerCan()
    {
        var owner = Coordinator();
        var target = Coordinator();
        var request = SeedEmailRequest(owner);
        var quote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(10), owner.Id);
        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, 100_000, "Per Visit")], null, null);
        quote.Send();
        api.SeedAs(db => db.Quotes.Add(quote));
        var coordinator = await SignedInAs(owner.Email);
        var manager = await SignedInAs("manager-a@example.com");

        var refused = await Transfer(coordinator, request.Id, target.Id);
        var allowed = await Transfer(manager, request.Id, target.Id);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("not_transferable", await CodeOf(refused));
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task TheTargetMustBeAnActiveCoordinatorOfTheSameTeamAndNotTheOwner()
    {
        var owner = Coordinator();
        var request = SeedEmailRequest(owner);
        var client = await SignedInAs(owner.Email);
        var inspector = api.AddUser($"transfer-target-inspector-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Inspector, api.ManagerA);
        var inactive = Coordinator();
        api.SeedAs(db => db.Users.Single(user => user.Id == inactive.Id).Deactivate());
        var otherTeam = Coordinator(api.ManagerB);

        var notACoordinator = await Transfer(client, request.Id, inspector.Id);
        var notActive = await Transfer(client, request.Id, inactive.Id);
        var same = await Transfer(client, request.Id, owner.Id);
        var foreign = await Transfer(client, request.Id, otherTeam.Id);
        var nobody = await Transfer(client, request.Id, Guid.NewGuid());

        Assert.Equal(("not_a_coordinator", HttpStatusCode.BadRequest), (await CodeOf(notACoordinator), notACoordinator.StatusCode));
        Assert.Equal(("coordinator_inactive", HttpStatusCode.BadRequest), (await CodeOf(notActive), notActive.StatusCode));
        Assert.Equal(("same_coordinator", HttpStatusCode.BadRequest), (await CodeOf(same), same.StatusCode));
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, nobody.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/requests/{request.Id}")).StatusCode);
    }

    [Fact]
    public async Task AFinishedRequestCannotBeTransferredEvenByTheManager()
    {
        var owner = Coordinator();
        var target = Coordinator();
        var request = InspectionRequest.Create("Done client", "Cairo", null, [RequestItem.Create("Crane", 1)], owner.Id, managerId: api.ManagerA);
        request.TransitionTo(JobStatus.QuoteRejected, owner.Id, "Not needed");
        api.SeedAs(db => db.Requests.Add(request));
        var manager = await SignedInAs("manager-a@example.com");

        var response = await Transfer(manager, request.Id, target.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("not_transferable", await CodeOf(response));
    }

    [Fact]
    public async Task TheCoordinatorListHasTheTeamsActiveCoordinatorsOnly()
    {
        var me = Coordinator();
        var colleague = Coordinator();
        var inactive = Coordinator();
        api.SeedAs(db => db.Users.Single(user => user.Id == inactive.Id).Deactivate());
        var foreigner = Coordinator(api.ManagerB);
        var inspector = api.AddUser($"transfer-list-inspector-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Inspector, api.ManagerA);
        var client = await SignedInAs(me.Email);

        var response = await client.GetAsync("/api/v1/team/coordinators");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains(colleague.Email, text);
        Assert.DoesNotContain(me.Email, text);
        Assert.DoesNotContain(inactive.Email, text);
        Assert.DoesNotContain(foreigner.Email, text);
        Assert.DoesNotContain(inspector.Email, text);

        var inspectorClient = await SignedInAs(inspector.Email);
        Assert.Equal(HttpStatusCode.Forbidden, (await inspectorClient.GetAsync("/api/v1/team/coordinators")).StatusCode);
    }
}
