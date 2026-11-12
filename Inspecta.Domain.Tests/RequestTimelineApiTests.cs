using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>GET /requests/{id}/timeline over HTTP: who may read it, the shape of the answer, and that another team never can.</summary>
public sealed class RequestTimelineApiTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 100;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public RequestTimelineApiTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.70.80." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    /// <summary>A request of team A that went from received to an accepted offer, with a coordinator of the team.</summary>
    private (InspectionRequest Request, AppUser Coordinator) SeedRequest()
    {
        var number = Interlocked.Increment(ref sequence);
        var coordinator = api.AddUser($"timeline-coordinator-{number}@example.com", UserRole.Coordinator, api.ManagerA);
        var request = InspectionRequest.Create($"Timeline client {number}", "Cairo", null, [RequestItem.Create("Mobile crane", 1)], coordinator.Id, managerId: api.ManagerA);
        request.TransitionTo(JobStatus.QuoteDraft, coordinator.Id);
        request.TransitionTo(JobStatus.QuoteSent, coordinator.Id);
        request.TransitionTo(JobStatus.Unassigned, coordinator.Id, "Client accepted");
        api.SeedAs(db => db.Requests.Add(request));
        return (request, coordinator);
    }

    [Fact]
    public async Task TheCoordinatorOfTheTeamGetsTheStagesAndTheEvents()
    {
        var (request, coordinator) = SeedRequest();
        var client = await SignedInAs(coordinator.Email);

        var response = await client.GetAsync($"/api/v1/requests/{request.Id}/timeline");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(request.Id, root.GetProperty("requestId").GetGuid());
        Assert.Equal(
            new[] { "quoting", "client_decision", "scheduling", "waiting_to_start", "inspection", "review", "delivery" },
            root.GetProperty("stages").EnumerateArray().Select(stage => stage.GetProperty("key").GetString()));
        var scheduling = root.GetProperty("stages").EnumerateArray().Single(stage => stage.GetProperty("key").GetString() == "scheduling");
        Assert.True(scheduling.GetProperty("inProgress").GetBoolean());
        var events = root.GetProperty("events").EnumerateArray().ToArray();
        Assert.Contains(events, item => item.GetProperty("title").GetString() == "Offer accepted, waiting for an inspector"
                                        && item.GetProperty("actor").GetProperty("id").GetGuid() == coordinator.Id);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("finishedAt").ValueKind);
        Assert.True(root.GetProperty("counters").GetProperty("certificatesReturned").GetInt32() == 0);
    }

    [Fact]
    public async Task TheManagerOfTheTeamAndTheAdminGetItToo()
    {
        var (request, _) = SeedRequest();
        var admin = api.AddUser($"timeline-admin-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Admin);

        var manager = await SignedInAs("manager-a@example.com");
        var administrator = await SignedInAs(admin.Email);

        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/api/v1/requests/{request.Id}/timeline")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await administrator.GetAsync($"/api/v1/requests/{request.Id}/timeline")).StatusCode);
    }

    [Fact]
    public async Task AnInspectorIsRefused()
    {
        var (request, _) = SeedRequest();
        var inspector = api.AddUser($"timeline-inspector-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Inspector, api.ManagerA);
        var client = await SignedInAs(inspector.Email);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/requests/{request.Id}/timeline")).StatusCode);
    }

    [Fact]
    public async Task ARequestOfAnotherTeamOrOneThatDoesNotExistIsNotFound()
    {
        var (request, _) = SeedRequest();
        var other = await SignedInAs("manager-b@example.com");

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/requests/{request.Id}/timeline")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/requests/{Guid.NewGuid()}/timeline")).StatusCode);
    }

    [Fact]
    public async Task NoOneSignedInGetsNothing()
    {
        var (request, _) = SeedRequest();

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync($"/api/v1/requests/{request.Id}/timeline")).StatusCode);
    }
}
