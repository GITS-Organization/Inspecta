using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>What the company admin is told about a coordinator's mailbox: whether it works, when it was last tried, and when mail last arrived.</summary>
public sealed class MailboxStatusApiTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 160;
    private static int sequence;
    private static readonly DateTimeOffset Checked = new(2026, 11, 16, 8, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Received = new(2026, 11, 16, 8, 12, 0, TimeSpan.Zero);
    private readonly ApiHttpTests.Api api;

    public MailboxStatusApiTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.77.120." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    private (AppUser Admin, AppUser Coordinator, Company Company) Seed()
    {
        var number = Interlocked.Increment(ref sequence);
        var company = Company.Create($"Mailbox status company {number}");
        api.SeedAs(db => db.Companies.Add(company));
        api.AddUser($"ms-manager-{number}@example.com", UserRole.TechnicalManager, company.Id);
        var admin = api.AddUser($"ms-admin-{number}@example.com", UserRole.CompanyAdmin, company.Id);
        var coordinator = api.AddUser($"ms-coordinator-{number}@example.com", UserRole.Coordinator, company.Id);
        return (admin, coordinator, company);
    }

    private static CoordinatorEmailConfig PasswordMailbox(AppUser coordinator, Company company) =>
        CoordinatorEmailConfig.Create(coordinator.Id, company.Id, "imap.example.com", 993, coordinator.Email, "encrypted", true);

    private async Task<JsonElement> MailboxOf(HttpClient admin, AppUser coordinator)
    {
        var response = await admin.GetAsync($"/api/v1/company/users/{coordinator.Id}/email-config");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    [Fact]
    public async Task A_mailbox_nobody_has_tried_has_no_check_and_no_email_dates()
    {
        var (admin, coordinator, company) = Seed();
        api.SeedAs(db => db.CoordinatorEmailConfigs.Add(PasswordMailbox(coordinator, company)));
        var client = await SignedInAs(admin.Email);

        var mailbox = await MailboxOf(client, coordinator);

        Assert.Equal("connected", mailbox.GetProperty("connectionStatus").GetString());
        Assert.Equal(JsonValueKind.Null, mailbox.GetProperty("lastCheckedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, mailbox.GetProperty("lastEmailAt").ValueKind);
    }

    [Fact]
    public async Task A_password_mailbox_whose_last_check_failed_is_a_problem_with_its_reason_and_dates()
    {
        var (admin, coordinator, company) = Seed();
        var config = PasswordMailbox(coordinator, company);
        config.RecordEmailReceived(Received);
        config.RecordFailedCheck(Checked, "The mailbox refused the email or password.", signInRefused: true);
        api.SeedAs(db => db.CoordinatorEmailConfigs.Add(config));
        var client = await SignedInAs(admin.Email);

        var mailbox = await MailboxOf(client, coordinator);

        Assert.Equal("problem", mailbox.GetProperty("connectionStatus").GetString());
        Assert.Equal("The mailbox refused the email or password.", mailbox.GetProperty("lastAuthError").GetString());
        Assert.Equal(Checked, mailbox.GetProperty("lastCheckedAt").GetDateTimeOffset());
        Assert.Equal(Received, mailbox.GetProperty("lastEmailAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task A_microsoft_mailbox_that_must_be_connected_again_still_says_needs_reconnect()
    {
        var (admin, coordinator, company) = Seed();
        var config = CoordinatorEmailConfig.CreateMicrosoft(coordinator.Id, company.Id, coordinator.Email, "token", "tenant");
        config.RecordFailedCheck(Checked, "Microsoft refused the saved sign-in.", signInRefused: true);
        api.SeedAs(db => db.CoordinatorEmailConfigs.Add(config));
        var client = await SignedInAs(admin.Email);

        var mailbox = await MailboxOf(client, coordinator);

        Assert.Equal("needs_reconnect", mailbox.GetProperty("connectionStatus").GetString());
        Assert.Equal("microsoft_oauth", mailbox.GetProperty("authMode").GetString());
    }
}
