using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>
/// What the company admin's screens (Users, Company, Identity) ask of the API, in the order a person uses them: every call goes through the
/// company paths, not the old team paths.
/// </summary>
public sealed class CompanyAdminScreensApiTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 120;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public CompanyAdminScreensApiTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.77.99." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    private (Company Company, AppUser Manager, AppUser Admin) SeedCompany()
    {
        var number = Interlocked.Increment(ref sequence);
        var company = Company.Create($"Screens company {number}");
        api.SeedAs(db => db.Companies.Add(company));
        var manager = api.AddUser($"sc-manager-{number}@example.com", UserRole.TechnicalManager, company.Id);
        var admin = api.AddUser($"sc-admin-{number}@example.com", UserRole.CompanyAdmin, company.Id);
        api.SeedAs(db => db.Companies.Single(item => item.Id == company.Id).SetDefaultReviewer(manager.Id));
        return (company, manager, admin);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    [Fact]
    public async Task The_users_screen_lists_people_creates_a_coordinator_with_its_mailbox_and_reads_and_moves_the_mailbox()
    {
        var (company, manager, admin) = SeedCompany();
        var client = await SignedInAs(admin.Email);
        var email = $"screens-coordinator-{Interlocked.Increment(ref sequence)}@example.com";

        var created = await client.PostAsJsonAsync("/api/v1/company/users", new
        {
            email, fullName = "Screens Coordinator", password = "StrongPassword123!", role = "coordinator",
            emailConfig = new { imapServer = "imap.example.com", imapPort = 993, emailUsername = email, password = "mail-secret-1" }
        });
        var coordinator = await Json(created);
        var coordinatorId = coordinator.GetProperty("id").GetGuid();
        var list = await Json(await client.GetAsync("/api/v1/company/users"));
        var mailbox = await client.GetAsync($"/api/v1/company/users/{coordinatorId}/email-config");
        var moved = await client.PutAsJsonAsync($"/api/v1/company/users/{coordinatorId}/email-config/sync-from", new { syncFrom = "2026-09-01T00:00:00+00:00" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Contains(list.EnumerateArray(), item => item.GetProperty("id").GetGuid() == manager.Id);
        Assert.DoesNotContain(list.EnumerateArray(), item => item.GetProperty("id").GetGuid() == admin.Id);
        Assert.All(list.EnumerateArray(), item => Assert.Equal(company.Id, item.GetProperty("managerId").GetGuid()));
        Assert.Equal(HttpStatusCode.OK, mailbox.StatusCode);
        var settings = await Json(mailbox);
        Assert.True(settings.GetProperty("configured").GetBoolean());
        Assert.Equal("imap.example.com", settings.GetProperty("imapServer").GetString());
        Assert.False(settings.TryGetProperty("password", out _));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.StartsWith("2026-09-01", (await Json(moved)).GetProperty("syncFrom").GetString());
    }

    [Fact]
    public async Task The_user_drawer_edits_leave_and_qualifications_changes_the_email_and_refuses_a_taken_one_and_a_weak_password()
    {
        var (_, manager, admin) = SeedCompany();
        var client = await SignedInAs(admin.Email);
        var number = Interlocked.Increment(ref sequence);
        var inspector = await Json(await client.PostAsJsonAsync("/api/v1/company/users", new
        {
            email = $"screens-inspector-{number}@example.com", fullName = "Screens Inspector", password = "StrongPassword123!", role = "inspector"
        }));
        var id = inspector.GetProperty("id").GetGuid();
        var back = DateTimeOffset.UtcNow.AddDays(5);

        var edited = await client.PutAsJsonAsync($"/api/v1/company/users/{id}", new
        {
            fullName = "Screens Inspector Renamed", qualifications = new[] { "Lifting equipment", "LEEA" }, isOnLeave = true, leaveUntil = back
        });
        var taken = await client.PutAsJsonAsync($"/api/v1/company/users/{id}/email", new { email = manager.Email });
        var weak = await client.PutAsJsonAsync($"/api/v1/company/users/{id}/password", new { newPassword = "abc" });
        var strong = await client.PutAsJsonAsync($"/api/v1/company/users/{id}/password", new { newPassword = "AnotherStrong123!" });
        var changed = await client.PutAsJsonAsync($"/api/v1/company/users/{id}/email", new { email = $"screens-moved-{number}@example.com" });

        var user = await Json(edited);
        Assert.Equal("Screens Inspector Renamed", user.GetProperty("fullName").GetString());
        Assert.True(user.GetProperty("isOnLeave").GetBoolean());
        Assert.Equal(["Lifting equipment", "LEEA"], user.GetProperty("qualifications").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, strong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
    }

    [Fact]
    public async Task A_person_with_no_records_can_be_switched_off_and_on_and_deleted_and_the_company_answers_for_the_admin()
    {
        var (company, _, admin) = SeedCompany();
        var client = await SignedInAs(admin.Email);
        var created = await Json(await client.PostAsJsonAsync("/api/v1/company/users", new
        {
            email = $"screens-delete-{Interlocked.Increment(ref sequence)}@example.com", fullName = "To Delete", password = "StrongPassword123!", role = "financial_manager"
        }));
        var id = created.GetProperty("id").GetGuid();

        var off = await client.PutAsJsonAsync($"/api/v1/company/users/{id}/status", new { isActive = false });
        var offUser = await Json(await client.GetAsync($"/api/v1/company/users/{id}"));
        var on = await client.PutAsJsonAsync($"/api/v1/company/users/{id}/status", new { isActive = true });
        var deleted = await client.DeleteAsync($"/api/v1/company/users/{id}");
        var gone = await client.GetAsync($"/api/v1/company/users/{id}");
        var overview = await Json(await client.GetAsync("/api/v1/company"));

        Assert.Equal(HttpStatusCode.NoContent, off.StatusCode);
        Assert.False(offUser.GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, on.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal(company.Id, overview.GetProperty("id").GetGuid());
        Assert.Equal("active", overview.GetProperty("subscriptionStatus").GetString());
        Assert.True(overview.TryGetProperty("subscriptionEndDate", out _));
    }
}
