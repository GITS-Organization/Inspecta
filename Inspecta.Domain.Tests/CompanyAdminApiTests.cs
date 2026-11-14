using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>
/// The company admin and the platform owner's companies: who manages accounts and identity, what a company admin cannot see, the two roles
/// that exist without rights yet, and how a company is created, switched off and handed over.
/// </summary>
public sealed class CompanyAdminApiTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 80;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public CompanyAdminApiTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private async Task<HttpClient> SignedInAs(string email, string? password = null)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.77.88." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = password ?? ApiHttpTests.Password });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"login of {email} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    private sealed record Setup(Company Company, AppUser Manager, AppUser Admin, AppUser Coordinator);

    private Setup SeedCompany()
    {
        var number = Interlocked.Increment(ref sequence);
        var company = Company.Create($"Admin company {number}");
        api.SeedAs(db => db.Companies.Add(company));
        var manager = api.AddUser($"ca-manager-{number}@example.com", UserRole.TechnicalManager, company.Id);
        var admin = api.AddUser($"ca-admin-{number}@example.com", UserRole.CompanyAdmin, company.Id);
        var coordinator = api.AddUser($"ca-coordinator-{number}@example.com", UserRole.Coordinator, company.Id);
        api.SeedAs(db => db.Companies.Single(item => item.Id == company.Id).SetDefaultReviewer(manager.Id));
        return new Setup(company, manager, admin, coordinator);
    }

    private AppUser PlatformOwner() => api.AddUser($"ca-owner-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Admin);

    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    [Fact]
    public async Task OnlyTheCompanyAdminManagesAccountsATechnicalManagerNoLonger()
    {
        var setup = SeedCompany();

        foreach (var path in new[] { "/api/v1/company/users", "/api/v1/manager/team" })
        {
            Assert.Equal(HttpStatusCode.OK, (await (await SignedInAs(setup.Admin.Email)).GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await (await SignedInAs(setup.Manager.Email)).GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await (await SignedInAs(setup.Coordinator.Email)).GetAsync(path)).StatusCode);
        }
    }

    [Fact]
    public async Task TheCompanyAdminCreatesEveryKindOfCompanyAccountButNotAnotherAdmin()
    {
        var setup = SeedCompany();
        var admin = await SignedInAs(setup.Admin.Email);
        var number = Interlocked.Increment(ref sequence);

        foreach (var role in new[] { "technical_manager", "general_manager", "financial_manager", "inspector" })
        {
            var created = await admin.PostAsJsonAsync("/api/v1/company/users", new
            {
                email = $"{role}-{number}@example.com", fullName = $"A {role}", password = "StrongPassword123!", role
            });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            Assert.Equal(setup.Company.Id, json.RootElement.GetProperty("managerId").GetGuid());
            Assert.Equal(setup.Admin.Id, json.RootElement.GetProperty("createdById").GetGuid());
        }

        foreach (var role in new[] { "company_admin", "admin" })
        {
            var refused = await admin.PostAsJsonAsync("/api/v1/company/users", new
            {
                email = $"{role}-{number}@example.com", fullName = "Not allowed", password = "StrongPassword123!", role
            });
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.Equal("role_forbidden", await CodeOf(refused));
        }

        var withMail = await admin.PostAsJsonAsync("/api/v1/company/users", new
        {
            email = $"inspector-mail-{number}@example.com", fullName = "Inspector", password = "StrongPassword123!", role = "inspector",
            emailConfig = new { imapServer = "imap.example.com", imapPort = 993, emailUsername = "x@example.com", password = "secret-1" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, withMail.StatusCode);
    }

    [Fact]
    public async Task TheFirstTechnicalManagerOfACompanyWithoutOneBecomesItsDefaultReviewer()
    {
        var number = Interlocked.Increment(ref sequence);
        var company = Company.Create($"No manager yet {number}");
        api.SeedAs(db => db.Companies.Add(company));
        var admin = api.AddUser($"fresh-admin-{number}@example.com", UserRole.CompanyAdmin, company.Id);
        var owner = await SignedInAs(admin.Email);

        var created = await owner.PostAsJsonAsync("/api/v1/company/users", new
        {
            email = $"first-manager-{number}@example.com", fullName = "First Manager", password = "StrongPassword123!", role = "technical_manager"
        });
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());

        var listed = await owner.GetAsync("/api/v1/company/technical-managers");
        var text = await listed.Content.ReadAsStringAsync();
        Assert.Contains(json.RootElement.GetProperty("id").GetString()!, text);
        Assert.Contains("\"isDefault\":true", text);
        var companyView = await owner.GetAsync("/api/v1/company");
        Assert.Contains(json.RootElement.GetProperty("id").GetString()!, await companyView.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheCompanyAdminSeesNoBusinessData()
    {
        var setup = SeedCompany();
        var admin = await SignedInAs(setup.Admin.Email);

        foreach (var path in new[]
                 {
                     "/api/v1/clients", "/api/v1/requests", "/api/v1/quotations", "/api/v1/quotation-queue", "/api/v1/equipment",
                     "/api/v1/dashboard/overview", "/api/v1/quotation-drafts", "/api/v1/inspectors", "/api/v1/team/coordinators"
                 })
            Assert.True((await admin.GetAsync(path)).StatusCode == HttpStatusCode.Forbidden, $"{path} should be closed to the company admin");
    }

    [Fact]
    public async Task ACompanyAdminReachesOnlyItsOwnCompany()
    {
        var mine = SeedCompany();
        var other = SeedCompany();
        var admin = await SignedInAs(mine.Admin.Email);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/company/users/{other.Coordinator.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/v1/company/users?managerId={other.Company.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/v1/company?companyId={other.Company.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync($"/api/v1/company/profile?companyId={other.Company.Id}", new { name = "Hijack", address = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/v1/admin/managers/{other.Company.Id}/company-profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/admin/companies")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/v1/company/users/{other.Coordinator.Id}/status", new { isActive = false })).StatusCode);
    }

    [Fact]
    public async Task TheCompanyAdminSetsPasswordsAndSwitchesAccountsButCannotSwitchOffAReviewerWithJobsWaiting()
    {
        var setup = SeedCompany();
        var admin = await SignedInAs(setup.Admin.Email);
        var request = InspectionRequest.Create("Waiting client", "Cairo", null, [RequestItem.Create("Crane", 1)], setup.Coordinator.Id, managerId: setup.Company.Id);
        request.UseReviewer(setup.Manager.Id);
        request.TransitionTo(JobStatus.QuoteDraft, setup.Coordinator.Id);
        request.TransitionTo(JobStatus.QuoteSent, setup.Coordinator.Id);
        request.TransitionTo(JobStatus.Unassigned, setup.Coordinator.Id);
        request.AssignInspector(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1), setup.Coordinator.Id);
        api.SeedAs(db => db.Requests.Add(request));

        var password = await admin.PutAsJsonAsync($"/api/v1/company/users/{setup.Manager.Id}/password", new { newPassword = "AnotherStrong-Pass-2" });
        Assert.Equal(HttpStatusCode.NoContent, password.StatusCode);
        var refused = await admin.PutAsJsonAsync($"/api/v1/company/users/{setup.Manager.Id}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("reviewer_has_pending_reviews", await CodeOf(refused));
        var deleteRefused = await admin.DeleteAsync($"/api/v1/company/users/{setup.Manager.Id}");
        Assert.Equal("reviewer_has_pending_reviews", await CodeOf(deleteRefused));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/company/users/{setup.Coordinator.Id}/status", new { isActive = false })).StatusCode);
    }

    [Fact]
    public async Task TheCompanyAdminEditsTheCompanysIdentityAndChoosesTheDefaultReviewer()
    {
        var setup = SeedCompany();
        var second = api.AddUser($"ca-second-{Interlocked.Increment(ref sequence)}@example.com", UserRole.TechnicalManager, setup.Company.Id);
        var admin = await SignedInAs(setup.Admin.Email);

        var saved = await admin.PutAsJsonAsync("/api/v1/company/profile", new { name = "Renamed Co.", address = "5 Industrial Zone, Cairo", primaryColor = "#184896", accentColor = "#34A046" });
        var read = await admin.GetAsync("/api/v1/company/profile");
        var chosen = await admin.PutAsJsonAsync("/api/v1/company/default-reviewer", new { reviewerId = second.Id });
        var notAManager = await admin.PutAsJsonAsync("/api/v1/company/default-reviewer", new { reviewerId = setup.Coordinator.Id });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Contains("Renamed Co.", await read.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, chosen.StatusCode);
        Assert.Contains(second.Id.ToString(), await chosen.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, notAManager.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await SignedInAs(setup.Manager.Email)).PutAsJsonAsync("/api/v1/company/default-reviewer", new { reviewerId = second.Id })).StatusCode);
    }

    [Fact]
    public async Task ThePlatformOwnerCreatesACompanyWithItsAdminAndCanSwitchItOffAndOn()
    {
        var owner = await SignedInAs(PlatformOwner().Email);
        var number = Interlocked.Increment(ref sequence);
        var email = $"handed-over-{number}@example.com";

        var created = await owner.PostAsJsonAsync("/api/v1/admin/companies", new
        {
            name = $"Handed over {number}", adminEmail = email, adminFullName = "Company Admin", adminPassword = ApiHttpTests.Password, subscriptionMonths = 6
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var companyId = json.RootElement.GetProperty("company").GetProperty("id").GetGuid();
        Assert.Equal("company_admin", json.RootElement.GetProperty("admin").GetProperty("role").GetString());
        Assert.Equal(companyId, json.RootElement.GetProperty("admin").GetProperty("managerId").GetGuid());
        var admin = await SignedInAs(email);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/company/users")).StatusCode);

        var off = await owner.PutAsJsonAsync($"/api/v1/admin/companies/{companyId}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/v1/company/users")).StatusCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SignedInAs(email));
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/v1/admin/companies/{companyId}/status", new { isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await SignedInAs(email)).GetAsync("/api/v1/company/users")).StatusCode);

        var renamed = await owner.PutAsJsonAsync($"/api/v1/admin/companies/{companyId}", new { name = "Renamed by the owner" });
        var suspended = await owner.PutAsJsonAsync($"/api/v1/admin/companies/{companyId}/subscription", new { subscriptionStatus = "suspended" });
        Assert.Contains("Renamed by the owner", await renamed.Content.ReadAsStringAsync());
        Assert.Contains("suspended", await suspended.Content.ReadAsStringAsync());
        Assert.Contains("Renamed by the owner", await (await owner.GetAsync("/api/v1/admin/companies")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ACompanyIsNotCreatedWithABadPasswordAnEmailInUseOrAMistakeInItsIdentity()
    {
        var owner = await SignedInAs(PlatformOwner().Email);
        var number = Interlocked.Increment(ref sequence);
        var before = await (await owner.GetAsync("/api/v1/admin/companies")).Content.ReadAsStringAsync();

        var weak = await owner.PostAsJsonAsync("/api/v1/admin/companies", new { name = $"Weak {number}", adminEmail = $"weak-{number}@example.com", adminFullName = "A", adminPassword = "short-1" });
        var taken = await owner.PostAsJsonAsync("/api/v1/admin/companies", new { name = $"Taken {number}", adminEmail = "manager-a@example.com", adminFullName = "A", adminPassword = ApiHttpTests.Password });
        var badProfile = await owner.PostAsJsonAsync("/api/v1/admin/companies", new
        {
            name = $"Profile {number}", adminEmail = $"profile-{number}@example.com", adminFullName = "A", adminPassword = ApiHttpTests.Password,
            companyProfile = new { name = "X", address = "Y", primaryColor = "blue" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badProfile.StatusCode);
        Assert.Equal(before, await (await owner.GetAsync("/api/v1/admin/companies")).Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("general_manager")]
    [InlineData("financial_manager")]
    public async Task TheGeneralAndFinancialManagersCanSignInAndOpenOnlyInvoicesAndNothingElse(string role)
    {
        var setup = SeedCompany();
        var user = api.AddUser($"reserved-{role}-{Interlocked.Increment(ref sequence)}@example.com", Enum.Parse<UserRole>(string.Concat(role.Split('_').Select(part => char.ToUpperInvariant(part[0]) + part[1..]))), setup.Company.Id);
        var client = await SignedInAs(user.Email);

        var me = await client.GetAsync("/api/v1/auth/me");
        using var json = JsonDocument.Parse(await me.Content.ReadAsStringAsync());

        Assert.Equal(role, json.RootElement.GetProperty("role").GetString());
        // Since invoices (2026-11-13) these two roles have rights: the general manager looks and sees revenue, the financial manager also records payments and closes.
        var expected = role == "general_manager" ? new[] { "Invoices.View", "Revenue.View", "Executive.View" } : new[] { "Invoices.View", "Invoices.RecordPayment", "Invoices.Close", "Revenue.View", "Finance.View" };
        Assert.Equal(expected.Order(), json.RootElement.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()!).Order());
        foreach (var path in new[] { "/api/v1/clients", "/api/v1/requests", "/api/v1/dashboard/overview", "/api/v1/company", "/api/v1/company/users", "/api/v1/equipment" })
            Assert.True((await client.GetAsync(path)).StatusCode == HttpStatusCode.Forbidden, $"{path} should be closed to {role}");
    }

    [Fact]
    public async Task TheAnswerOfMeListsWhatEachRoleMayDo()
    {
        var setup = SeedCompany();

        async Task<string[]> PermissionsOf(string email)
        {
            using var json = JsonDocument.Parse(await (await (await SignedInAs(email)).GetAsync("/api/v1/auth/me")).Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()!).ToArray();
        }

        var coordinator = await PermissionsOf(setup.Coordinator.Email);
        var manager = await PermissionsOf(setup.Manager.Email);
        var admin = await PermissionsOf(setup.Admin.Email);

        Assert.Contains("Requests.Work", coordinator);
        Assert.Contains("Requests.ChangeReviewer", coordinator);
        Assert.DoesNotContain("Accounts.Manage", coordinator);
        Assert.Contains("Review.Act", manager);
        Assert.DoesNotContain("Accounts.Manage", manager);
        Assert.Contains("Accounts.Manage", admin);
        Assert.Contains("Company.EditIdentity", admin);
        Assert.DoesNotContain("Requests.View", admin);
        Assert.DoesNotContain("Review.Act", admin);
    }
}
