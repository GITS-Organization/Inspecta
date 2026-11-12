using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The reviewer over HTTP: the default every request starts with, the dropdown, the change, and who may approve or return a job.</summary>
public sealed class ReviewerApiTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 60;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public ReviewerApiTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.55.66." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"login of {email} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    private sealed record Setup(Company Company, AppUser First, AppUser Second, AppUser Coordinator, AppUser Inspector, AppUser Admin);

    /// <summary>A company with its own id, two technical managers (the first is the default reviewer), a coordinator and an inspector.</summary>
    private Setup SeedCompany()
    {
        var number = Interlocked.Increment(ref sequence);
        var company = Company.Create($"Reviewers {number}");
        api.SeedAs(db => db.Companies.Add(company));
        var first = api.AddUser($"rev-first-{number}@example.com", UserRole.TechnicalManager, company.Id);
        var second = api.AddUser($"rev-second-{number}@example.com", UserRole.TechnicalManager, company.Id);
        var coordinator = api.AddUser($"rev-coordinator-{number}@example.com", UserRole.Coordinator, company.Id);
        var inspector = api.AddUser($"rev-inspector-{number}@example.com", UserRole.Inspector, company.Id);
        var admin = api.AddUser($"rev-admin-{number}@example.com", UserRole.CompanyAdmin, company.Id);
        api.SeedAs(db => db.Companies.Single(item => item.Id == company.Id).SetDefaultReviewer(first.Id));
        return new Setup(company, first, second, coordinator, inspector, admin);
    }

    private InspectionRequest SeedRequest(Setup setup, Action<InspectionRequest>? shape = null)
    {
        var request = InspectionRequest.Create(
            $"Reviewed client {Interlocked.Increment(ref sequence)}", "Cairo", null, [RequestItem.Create("Crane", 1)], setup.Coordinator.Id, managerId: setup.Company.Id);
        request.UseReviewer(setup.First.Id);
        shape?.Invoke(request);
        api.SeedAs(db => db.Requests.Add(request));
        return request;
    }

    private static void ReachReview(InspectionRequest request, Guid coordinatorId, Guid inspectorId)
    {
        request.TransitionTo(JobStatus.QuoteDraft, coordinatorId);
        request.TransitionTo(JobStatus.QuoteSent, coordinatorId);
        request.TransitionTo(JobStatus.Unassigned, coordinatorId);
        request.AssignInspector(inspectorId, DateTimeOffset.UtcNow.AddDays(1), coordinatorId);
        request.TransitionTo(JobStatus.TaskAccepted, inspectorId);
        request.TransitionTo(JobStatus.InProgress, inspectorId);
        request.TransitionTo(JobStatus.CertificateSubmitted, inspectorId);
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static Task<HttpResponseMessage> ChangeTo(HttpClient client, Guid requestId, Guid reviewerId, string? note = null) =>
        client.PutAsJsonAsync($"/api/v1/requests/{requestId}/reviewer", new { reviewerId, note });

    [Fact]
    public async Task ANewRequestStartsWithTheCompanysDefaultReviewerAndNamesHim()
    {
        var setup = SeedCompany();
        var coordinator = await SignedInAs(setup.Coordinator.Email);

        var created = await coordinator.PostAsJsonAsync("/api/v1/requests", new { clientName = "Acme Steel", location = "Cairo", items = new[] { new { name = "Crane", quantity = 1 } } });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal(setup.First.Id, json.RootElement.GetProperty("reviewerId").GetGuid());
        var read = await coordinator.GetAsync($"/api/v1/requests/{json.RootElement.GetProperty("id").GetGuid()}");
        using var again = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        Assert.Equal(setup.First.FullName, again.RootElement.GetProperty("reviewerName").GetString());
    }

    [Fact]
    public async Task TheDropdownListsTheTechnicalManagersOfTheCompanyWithTheDefaultMarked()
    {
        var setup = SeedCompany();
        var other = SeedCompany();
        var admin = api.AddUser($"rev-admin-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Admin);

        foreach (var email in new[] { setup.Coordinator.Email, setup.Inspector.Email, setup.First.Email })
        {
            var response = await (await SignedInAs(email)).GetAsync("/api/v1/company/technical-managers");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var rows = json.RootElement.EnumerateArray().ToArray();
            Assert.Equal(2, rows.Length);
            Assert.True(rows.Single(row => row.GetProperty("id").GetGuid() == setup.First.Id).GetProperty("isDefault").GetBoolean());
            Assert.False(rows.Single(row => row.GetProperty("id").GetGuid() == setup.Second.Id).GetProperty("isDefault").GetBoolean());
            Assert.DoesNotContain(rows, row => row.GetProperty("id").GetGuid() == other.First.Id);
        }

        var platform = await SignedInAs(admin.Email);
        Assert.Equal("[]", (await (await platform.GetAsync("/api/v1/company/technical-managers")).Content.ReadAsStringAsync()).Trim());
        Assert.Contains(other.First.Email, await (await platform.GetAsync($"/api/v1/company/technical-managers?companyId={other.Company.Id}")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheCoordinatorChangesTheReviewerAndItIsWrittenInTheHistoryAndTheTimeline()
    {
        var setup = SeedCompany();
        var request = SeedRequest(setup);
        var coordinator = await SignedInAs(setup.Coordinator.Email);

        var response = await ChangeTo(coordinator, request.Id, setup.Second.Id, "First one is away");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(setup.Second.Id, json.RootElement.GetProperty("reviewerId").GetGuid());
        Assert.Contains("Reviewer changed from", await response.Content.ReadAsStringAsync());
        var timeline = await (await coordinator.GetAsync($"/api/v1/requests/{request.Id}/timeline")).Content.ReadAsStringAsync();
        using var events = JsonDocument.Parse(timeline);
        Assert.Contains(events.RootElement.GetProperty("events").EnumerateArray(), item => item.GetProperty("title").GetString() == "Reviewer changed");
        Assert.Equal(0, events.RootElement.GetProperty("counters").GetProperty("inspectorChanges").GetInt32());
    }

    [Fact]
    public async Task OnlyTheOwningCoordinatorAndThePlatformOwnerChooseTheReviewerAndOnlyAmongTheCompanysTechnicalManagers()
    {
        var setup = SeedCompany();
        var elsewhere = SeedCompany();
        var request = SeedRequest(setup);
        var inactive = api.AddUser($"rev-off-{Interlocked.Increment(ref sequence)}@example.com", UserRole.TechnicalManager, setup.Company.Id);
        api.SeedAs(db => db.Users.Single(user => user.Id == inactive.Id).Deactivate());
        var emailRequest = InspectionRequest.CreateFromEmail(
            setup.Company.Id, setup.Coordinator.Id,
            new IncomingEmail("1", Interlocked.Increment(ref sequence), "Quote please", null, "buyer@acme.com", "Buyer", "Crane", [], [], [], [], [], $"<{Guid.NewGuid()}@acme.com>", null, null, DateTimeOffset.UtcNow),
            clientName: "Acme", location: "Cairo");
        api.SeedAs(db => db.Requests.Add(emailRequest));
        var otherCoordinator = api.AddUser($"rev-other-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Coordinator, setup.Company.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await ChangeTo(await SignedInAs(setup.First.Email), request.Id, setup.Second.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ChangeTo(await SignedInAs(setup.Inspector.Email), request.Id, setup.Second.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ChangeTo(await SignedInAs(otherCoordinator.Email), emailRequest.Id, setup.Second.Id)).StatusCode);

        var owner = await SignedInAs(setup.Coordinator.Email);
        Assert.Equal(HttpStatusCode.NotFound, (await ChangeTo(owner, request.Id, elsewhere.First.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ChangeTo(owner, request.Id, Guid.NewGuid())).StatusCode);
        var notAManager = await ChangeTo(owner, request.Id, setup.Inspector.Id);
        Assert.Equal(("reviewer_invalid", HttpStatusCode.BadRequest), (await CodeOf(notAManager), notAManager.StatusCode));
        var off = await ChangeTo(owner, request.Id, inactive.Id);
        Assert.Equal("reviewer_invalid", await CodeOf(off));
        var same = await ChangeTo(owner, request.Id, setup.First.Id);
        Assert.Equal(("reviewer_not_changeable", HttpStatusCode.Conflict), (await CodeOf(same), same.StatusCode));
    }

    [Fact]
    public async Task OnlyTheJobsReviewerCanApproveOrReturnItAndThePlatformOwnerAlways()
    {
        var setup = SeedCompany();
        var request = SeedRequest(setup, item => ReachReview(item, setup.Coordinator.Id, setup.Inspector.Id));
        var admin = api.AddUser($"rev-owner-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Admin);
        var notTheReviewer = await SignedInAs(setup.Second.Email);
        var reviewer = await SignedInAs(setup.First.Email);
        var owner = await SignedInAs(admin.Email);
        var approve = $"/api/v1/quotation-queue/{request.Id}/approve-all";
        var giveBack = $"/api/v1/quotation-queue/{request.Id}/return-to-inspector";

        var refusedApprove = await notTheReviewer.PostAsJsonAsync(approve, new { });
        var refusedReturn = await notTheReviewer.PostAsJsonAsync(giveBack, new { note = "Fix the serial" });

        Assert.Equal(HttpStatusCode.Forbidden, refusedApprove.StatusCode);
        Assert.Equal("not_the_reviewer", await CodeOf(refusedApprove));
        Assert.Equal("not_the_reviewer", await CodeOf(refusedReturn));
        // The reviewer and the platform owner get past the check: what answers next is about the job itself, not about who they are.
        foreach (var allowed in new[] { reviewer, owner })
        {
            var answer = await allowed.PostAsJsonAsync(approve, new { });
            Assert.NotEqual("not_the_reviewer", await CodeOf(answer));
        }

        var returned = await reviewer.PostAsJsonAsync(giveBack, new { note = "Fix the serial" });
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
    }

    [Fact]
    public async Task AssigningAJobCanNameTheReviewerAndWithoutItTheCurrentOneStays()
    {
        var setup = SeedCompany();
        void ToUnassigned(InspectionRequest item)
        {
            item.TransitionTo(JobStatus.QuoteDraft, setup.Coordinator.Id);
            item.TransitionTo(JobStatus.QuoteSent, setup.Coordinator.Id);
            item.TransitionTo(JobStatus.Unassigned, setup.Coordinator.Id);
        }
        var named = SeedRequest(setup, ToUnassigned);
        var plain = SeedRequest(setup, ToUnassigned);
        var coordinator = await SignedInAs(setup.Coordinator.Email);
        var when = DateTimeOffset.UtcNow.AddDays(2);

        var withReviewer = await coordinator.PostAsJsonAsync(
            $"/api/v1/requests/{named.Id}/assignment", new { inspectorId = setup.Inspector.Id, scheduledAt = when, reviewerId = setup.Second.Id });
        var withoutReviewer = await coordinator.PostAsJsonAsync(
            $"/api/v1/requests/{plain.Id}/assignment", new { inspectorId = setup.Inspector.Id, scheduledAt = when });

        Assert.Equal(HttpStatusCode.OK, withReviewer.StatusCode);
        using var one = JsonDocument.Parse(await withReviewer.Content.ReadAsStringAsync());
        Assert.Equal(setup.Second.Id, one.RootElement.GetProperty("reviewerId").GetGuid());
        using var two = JsonDocument.Parse(await withoutReviewer.Content.ReadAsStringAsync());
        Assert.Equal(setup.First.Id, two.RootElement.GetProperty("reviewerId").GetGuid());
    }

    [Fact]
    public async Task ADirectCertificateTakesTheDefaultReviewerAndTheCreatorCanPickAnother()
    {
        var setup = SeedCompany();
        var client = Client.Create($"Direct client {Interlocked.Increment(ref sequence)}", managerId: setup.Company.Id);
        api.SeedAs(db => db.Clients.Add(client));
        var manager = await SignedInAs(setup.First.Email);

        var plain = await manager.PostAsJsonAsync("/api/v1/certifications", new { clientId = client.Id, machines = new[] { new { name = "Forklift Truck", quantity = 1 } } });
        var picked = await manager.PostAsJsonAsync("/api/v1/certifications", new { clientId = client.Id, reviewerId = setup.Second.Id, machines = new[] { new { name = "Forklift Truck", quantity = 1 } } });

        Assert.Equal(HttpStatusCode.Created, plain.StatusCode);
        Assert.Equal(HttpStatusCode.Created, picked.StatusCode);
        using var one = JsonDocument.Parse(await plain.Content.ReadAsStringAsync());
        using var two = JsonDocument.Parse(await picked.Content.ReadAsStringAsync());
        Assert.Equal(setup.First.Id, one.RootElement.GetProperty("request").GetProperty("reviewerId").GetGuid());
        Assert.Equal(setup.Second.Id, two.RootElement.GetProperty("request").GetProperty("reviewerId").GetGuid());
    }

    [Fact]
    public async Task ATechnicalManagerWithJobsToReviewCannotBeDeletedAndTheDefaultMovesWhenOneLeaves()
    {
        var setup = SeedCompany();
        var admin = api.AddUser($"rev-remover-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Admin);
        var owner = await SignedInAs(admin.Email);
        var early = SeedRequest(setup);
        var busy = SeedRequest(setup, item => ReachReview(item, setup.Coordinator.Id, setup.Inspector.Id));

        var refused = await owner.DeleteAsync($"/api/v1/users/{setup.First.Id}");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("reviewer_has_pending_reviews", await CodeOf(refused));
        Assert.Contains(busy.Id.ToString(), await refused.Content.ReadAsStringAsync());

        // Hand the busy job to the other reviewer: the first can now leave, and the default and the unstarted request follow.
        var coordinator = await SignedInAs(setup.Coordinator.Email);
        Assert.Equal(HttpStatusCode.OK, (await ChangeTo(coordinator, busy.Id, setup.Second.Id)).StatusCode);
        var removed = await owner.DeleteAsync($"/api/v1/users/{setup.First.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        var list = await (await SignedInAs(setup.Coordinator.Email)).GetAsync("/api/v1/company/technical-managers");
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var only = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(setup.Second.Id, only.GetProperty("id").GetGuid());
        Assert.True(only.GetProperty("isDefault").GetBoolean());
        var moved = await (await SignedInAs(setup.Coordinator.Email)).GetAsync($"/api/v1/requests/{early.Id}");
        using var movedJson = JsonDocument.Parse(await moved.Content.ReadAsStringAsync());
        Assert.Equal(setup.Second.Id, movedJson.RootElement.GetProperty("reviewerId").GetGuid());
    }

    [Fact]
    public async Task ACompanyWithItsOwnIdCanHaveACoordinatorWithMailSettingsAndTheSettingsCanBeChanged()
    {
        var setup = SeedCompany();
        var manager = await SignedInAs(setup.Admin.Email);
        var email = $"new-coordinator-{Interlocked.Increment(ref sequence)}@example.com";

        var created = await manager.PostAsJsonAsync("/api/v1/manager/team", new
        {
            email,
            fullName = "New Coordinator",
            password = "StrongPassword123!",
            role = "coordinator",
            emailConfig = new { imapServer = "imap.example.com", imapPort = 993, emailUsername = email, password = "mail-secret-1" }
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var coordinatorId = json.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(setup.Company.Id, json.RootElement.GetProperty("managerId").GetGuid());
        Assert.Equal(setup.Admin.Id, json.RootElement.GetProperty("createdById").GetGuid());
        var changed = await manager.PutAsJsonAsync($"/api/v1/manager/team/{coordinatorId}/email-config", new
        {
            imapServer = "imap.other.com", imapPort = 993, emailUsername = email, password = "mail-secret-2"
        });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
    }

    [Fact]
    public async Task APlatformOwnerCanEditTheProfileOfATechnicalManagerWhoseCompanyIsTheirOwnId()
    {
        var number = Interlocked.Increment(ref sequence);
        var manager = api.AddUser($"backfilled-{number}@example.com", UserRole.TechnicalManager);
        // The shape the migration leaves a technical manager in: attached to the company that carries their own id.
        api.SeedAs(db => db.Database.ExecuteSqlInterpolated($"UPDATE \"Users\" SET \"ManagerId\" = \"Id\" WHERE \"Id\" = {manager.Id}"));
        var admin = api.AddUser($"profile-admin-{number}@example.com", UserRole.Admin);
        var owner = await SignedInAs(admin.Email);

        var response = await owner.PutAsJsonAsync($"/api/v1/users/{manager.Id}/profile", new { fullName = "Renamed Manager", role = "technical_manager" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Renamed Manager", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ATechnicalManagerCreatedByThePlatformOwnerStartsACompanyWithTheirOwnIdAndIsItsDefaultReviewer()
    {
        var number = Interlocked.Increment(ref sequence);
        var admin = api.AddUser($"creator-admin-{number}@example.com", UserRole.Admin);
        var owner = await SignedInAs(admin.Email);
        var email = $"fresh-manager-{number}@example.com";

        var created = await owner.PostAsJsonAsync("/api/v1/technical-managers", new
        {
            email, fullName = "Fresh Manager", password = ApiHttpTests.Password, subscriptionMonths = 6
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var managerId = json.RootElement.GetProperty("id").GetGuid();
        // Routes that name the company by its first manager keep working: the company has the manager's id.
        var subscription = await owner.PutAsJsonAsync($"/api/v1/technical-managers/{managerId}/subscription", new { subscriptionStatus = "active", subscriptionEndDate = DateTimeOffset.UtcNow.AddMonths(2) });
        Assert.Equal(HttpStatusCode.OK, subscription.StatusCode);
        var listed = await owner.GetAsync($"/api/v1/company/technical-managers?companyId={managerId}");
        Assert.Contains(managerId.ToString(), await listed.Content.ReadAsStringAsync());
        Assert.Contains("\"isDefault\":true", await listed.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await SignedInAs(email)).GetAsync("/api/v1/clients").Result.StatusCode);
    }
}
