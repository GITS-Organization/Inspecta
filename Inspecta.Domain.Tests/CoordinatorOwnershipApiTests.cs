using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>
/// A coordinator sees and works on only the requests they own, manual or email. Managers still see the whole team, and handing a request
/// to another coordinator (the transfer) moves it from one to the other.
/// </summary>
public sealed class CoordinatorOwnershipApiTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 220;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public CoordinatorOwnershipApiTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.90.30." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    private AppUser Coordinator() =>
        api.AddUser($"ownership-coordinator-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Coordinator, api.ManagerA);

    /// <summary>A manual request of the team with an accepted quotation, waiting for an inspector.</summary>
    private InspectionRequest SeedAcceptedManualRequest(AppUser owner)
    {
        var request = InspectionRequest.Create(
            $"Manual client {Interlocked.Increment(ref sequence)}", "Cairo", null, [RequestItem.Create("Crane", 1)], owner.Id, managerId: api.ManagerA);
        request.TransitionTo(JobStatus.QuoteDraft, owner.Id);
        request.TransitionTo(JobStatus.QuoteSent, owner.Id);
        request.TransitionTo(JobStatus.Unassigned, owner.Id);
        var quote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(10), owner.Id, clientName: request.ClientName);
        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, 100_000, "Per Visit", "Crane inspection")], null, null);
        quote.Send();
        quote.RecordClientDecision(QuoteStatus.Accepted);
        api.SeedAs(db =>
        {
            db.Requests.Add(request);
            db.Quotes.Add(quote);
        });
        return request;
    }


    [Fact]
    public async Task AnotherCoordinatorOfTheTeamDoesNotSeeAManualRequestOrItsAcceptedQuotation()
    {
        var owner = Coordinator();
        var other = Coordinator();
        var request = SeedAcceptedManualRequest(owner);
        var mine = await SignedInAs(owner.Email);
        var theirs = await SignedInAs(other.Email);

        Assert.Equal(HttpStatusCode.OK, (await mine.GetAsync($"/api/v1/requests/{request.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await theirs.GetAsync($"/api/v1/requests/{request.Id}")).StatusCode);
    }

    [Fact]
    public async Task TheManagerStillSeesTheWholeTeam()
    {
        var owner = Coordinator();
        var request = SeedAcceptedManualRequest(owner);
        var manager = await SignedInAs("manager-a@example.com");

        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/api/v1/requests/{request.Id}")).StatusCode);
    }

    [Fact]
    public async Task HandingTheRequestToAnotherCoordinatorMovesItFromOneToTheOther()
    {
        var first = Coordinator();
        var second = Coordinator();
        var request = SeedAcceptedManualRequest(first);
        var manager = await SignedInAs("manager-a@example.com");

        // The owner can no longer transfer after a quotation was sent; the manager can at any time before the request is finished.
        var response = await manager.PostAsJsonAsync($"/api/v1/requests/{request.Id}/transfer", new { coordinatorId = second.Id, note = "Sara is on leave" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var one = await SignedInAs(first.Email);
        var two = await SignedInAs(second.Email);
        Assert.Equal(HttpStatusCode.NotFound, (await one.GetAsync($"/api/v1/requests/{request.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await two.GetAsync($"/api/v1/requests/{request.Id}")).StatusCode);
    }
}
