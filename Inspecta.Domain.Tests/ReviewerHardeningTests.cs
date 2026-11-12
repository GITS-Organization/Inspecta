using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The edges of the reviewer rules found in review: who may pick one, jobs that start without one, leaving, and the old review route.</summary>
public sealed class ReviewerHardeningTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 120;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public ReviewerHardeningTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.99.11." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"login of {email} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private sealed record Setup(Company Company, AppUser First, AppUser Second, AppUser Coordinator, AppUser Inspector);

    private Setup SeedCompany(bool withManagers = true)
    {
        var number = Interlocked.Increment(ref sequence);
        var company = Company.Create($"Hardening {number}");
        api.SeedAs(db => db.Companies.Add(company));
        var first = withManagers ? api.AddUser($"hard-first-{number}@example.com", UserRole.TechnicalManager, company.Id) : null!;
        var second = withManagers ? api.AddUser($"hard-second-{number}@example.com", UserRole.TechnicalManager, company.Id) : null!;
        var coordinator = api.AddUser($"hard-coordinator-{number}@example.com", UserRole.Coordinator, company.Id);
        var inspector = api.AddUser($"hard-inspector-{number}@example.com", UserRole.Inspector, company.Id);
        if (withManagers) api.SeedAs(db => db.Companies.Single(item => item.Id == company.Id).SetDefaultReviewer(first.Id));
        return new Setup(company, first, second, coordinator, inspector);
    }

    private InspectionRequest SeedUnassigned(Setup setup)
    {
        var request = InspectionRequest.Create(
            $"Hardening client {Interlocked.Increment(ref sequence)}", "Cairo", null, [RequestItem.Create("Crane", 1)], setup.Coordinator.Id, managerId: setup.Company.Id);
        if (setup.First is not null) request.UseReviewer(setup.First.Id);
        request.TransitionTo(JobStatus.QuoteDraft, setup.Coordinator.Id);
        request.TransitionTo(JobStatus.QuoteSent, setup.Coordinator.Id);
        request.TransitionTo(JobStatus.Unassigned, setup.Coordinator.Id);
        api.SeedAs(db => db.Requests.Add(request));
        return request;
    }

    [Fact]
    public async Task ATechnicalManagerCanAssignButCannotMakeThemselvesTheReviewer()
    {
        var setup = SeedCompany();
        var request = SeedUnassigned(setup);
        var manager = await SignedInAs(setup.Second.Email);
        var when = DateTimeOffset.UtcNow.AddDays(2);

        var grab = await manager.PostAsJsonAsync($"/api/v1/requests/{request.Id}/assignment", new { inspectorId = setup.Inspector.Id, scheduledAt = when, reviewerId = setup.Second.Id });
        var plain = await manager.PostAsJsonAsync($"/api/v1/requests/{request.Id}/assignment", new { inspectorId = setup.Inspector.Id, scheduledAt = when });

        Assert.Equal(HttpStatusCode.Forbidden, grab.StatusCode);
        Assert.Equal(HttpStatusCode.OK, plain.StatusCode);
        using var json = JsonDocument.Parse(await plain.Content.ReadAsStringAsync());
        Assert.Equal(setup.First.Id, json.RootElement.GetProperty("reviewerId").GetGuid());
        Assert.Equal(setup.First.FullName, json.RootElement.GetProperty("reviewerName").GetString());
    }

    [Fact]
    public async Task AManDayOrADirectCertificateCannotStartInACompanyWithNoTechnicalManager()
    {
        var setup = SeedCompany(withManagers: false);
        var client = Client.Create($"Lonely {Interlocked.Increment(ref sequence)}", managerId: setup.Company.Id);
        api.SeedAs(db => db.Clients.Add(client));
        var owner = api.AddUser($"hard-owner-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Admin);

        var manDay = await (await SignedInAs(setup.Coordinator.Email)).PostAsJsonAsync(
            "/api/v1/requests/man-day", new { clientId = client.Id, inspectorId = setup.Inspector.Id, scheduledAt = DateTimeOffset.UtcNow.AddDays(1) });
        var direct = await (await SignedInAs(owner.Email)).PostAsJsonAsync(
            "/api/v1/certifications", new { managerId = setup.Company.Id, clientId = client.Id, machines = new[] { new { name = "Forklift Truck", quantity = 1 } } });

        Assert.Equal(("no_reviewer", HttpStatusCode.Conflict), (await CodeOf(manDay), manDay.StatusCode));
        Assert.Equal(("no_reviewer", HttpStatusCode.Conflict), (await CodeOf(direct), direct.StatusCode));
    }

    [Fact]
    public async Task OnlyTheReviewerCanUseTheOldReviewRoute()
    {
        var setup = SeedCompany();
        var request = SeedUnassigned(setup);
        var notTheReviewer = await SignedInAs(setup.Second.Email);

        var refused = await notTheReviewer.PostAsJsonAsync(
            $"/api/v1/requests/{request.Id}/certificates/{Guid.NewGuid()}/review", new { approve = false, reviewerComments = "no" });

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("not_the_reviewer", await CodeOf(refused));
    }

    [Fact]
    public async Task ACompanyAdminCannotTouchThePlatformOwnerOrAnotherCompanyAdminAndCannotPromoteAnyone()
    {
        var setup = SeedCompany();
        var number = Interlocked.Increment(ref sequence);
        var admin = api.AddUser($"hard-admin-{number}@example.com", UserRole.CompanyAdmin, setup.Company.Id);
        var colleagueAdmin = api.AddUser($"hard-admin2-{number}@example.com", UserRole.CompanyAdmin, setup.Company.Id);
        var owner = api.AddUser($"hard-platform-{number}@example.com", UserRole.Admin);
        var client = await SignedInAs(admin.Email);

        foreach (var target in new[] { owner.Id, colleagueAdmin.Id, admin.Id })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/v1/company/users/{target}/password", new { newPassword = "AnotherStrong-Pass-9" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/v1/company/users/{target}/email", new { email = $"x-{target}@example.com" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/v1/company/users/{target}/status", new { isActive = false })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/company/users/{target}")).StatusCode);
        }

        var promote = await client.PutAsJsonAsync($"/api/v1/company/users/{setup.Coordinator.Id}", new { fullName = "Same Person", role = "admin" });
        var after = await client.GetAsync($"/api/v1/company/users/{setup.Coordinator.Id}");
        Assert.Equal("coordinator", JsonDocument.Parse(await after.Content.ReadAsStringAsync()).RootElement.GetProperty("role").GetString());
        Assert.True(promote.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WhenANonDefaultTechnicalManagerLeavesTheirUnstartedRequestsGoToTheCompanyDefault()
    {
        var setup = SeedCompany();
        var number = Interlocked.Increment(ref sequence);
        var third = api.AddUser($"hard-third-{number}@example.com", UserRole.TechnicalManager, setup.Company.Id);
        var companyAdmin = api.AddUser($"hard-boss-{number}@example.com", UserRole.CompanyAdmin, setup.Company.Id);
        var owner = api.AddUser($"hard-remover-{number}@example.com", UserRole.Admin);
        var boss = await SignedInAs(companyAdmin.Email);
        // The company default is the second manager, who is not the oldest.
        Assert.Equal(HttpStatusCode.OK, (await boss.PutAsJsonAsync("/api/v1/company/default-reviewer", new { reviewerId = setup.Second.Id })).StatusCode);
        var request = InspectionRequest.Create("Unstarted client", "Cairo", null, [RequestItem.Create("Crane", 1)], setup.Coordinator.Id, managerId: setup.Company.Id);
        request.UseReviewer(third.Id);
        api.SeedAs(db => db.Requests.Add(request));

        var removed = await (await SignedInAs(owner.Email)).DeleteAsync($"/api/v1/users/{third.Id}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        var read = await (await SignedInAs(setup.Coordinator.Email)).GetAsync($"/api/v1/requests/{request.Id}");
        using var json = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        Assert.Equal(setup.Second.Id, json.RootElement.GetProperty("reviewerId").GetGuid());
    }
}
