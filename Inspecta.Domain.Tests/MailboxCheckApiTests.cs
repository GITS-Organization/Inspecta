using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Inspecta.Application.Contracts.Email;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Inspecta.Domain.Tests;

/// <summary>"Test connection" (settings typed in, nothing saved) and "Check now" (the saved mailbox), over HTTP with a probe that never touches the network.</summary>
public sealed class MailboxCheckApiTests : IClassFixture<ApiHttpTests.Api>
{
    private static readonly string Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static int caller = 200;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public MailboxCheckApiTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private sealed class FakeProbe : IMailboxProbe
    {
        public string? FailAt { get; set; }
        public int Calls { get; private set; }
        public MailboxProbeSettings? Seen { get; private set; }

        public Task<IReadOnlyList<MailboxCheckStep>> ProbeAsync(MailboxProbeSettings settings, CancellationToken cancellationToken = default)
        {
            Calls++;
            Seen = settings;
            var failed = false;
            var steps = new List<MailboxCheckStep>();
            foreach (var step in new[] { "reach", "sign_in", "read", "send" })
            {
                if (failed) steps.Add(new MailboxCheckStep(step, "skipped"));
                else if (step == FailAt)
                {
                    failed = true;
                    steps.Add(new MailboxCheckStep(step, "failed", $"{step} did not work.", "Try this."));
                }
                else steps.Add(new MailboxCheckStep(step, "ok"));
            }
            return Task.FromResult<IReadOnlyList<MailboxCheckStep>>(steps);
        }
    }

    private WebApplicationFactory<Program> Host(FakeProbe probe) =>
        api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("IMAP_ENCRYPTION_KEY", Key);
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(value => value.ServiceType == typeof(IMailboxProbe)).ToList()) services.Remove(descriptor);
                services.AddSingleton<IMailboxProbe>(probe);
            });
        });

    private async Task<HttpClient> SignedInAs(WebApplicationFactory<Program> host, string email)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.77.130." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    private sealed record World(Company Company, AppUser Admin, AppUser Manager, AppUser Coordinator);

    private World Seed()
    {
        var number = Interlocked.Increment(ref sequence);
        var company = Company.Create($"Mailbox check company {number}");
        api.SeedAs(db => db.Companies.Add(company));
        var manager = api.AddUser($"mc-manager-{number}@example.com", UserRole.TechnicalManager, company.Id);
        var admin = api.AddUser($"mc-admin-{number}@example.com", UserRole.CompanyAdmin, company.Id);
        var coordinator = api.AddUser($"mc-coordinator-{number}@example.com", UserRole.Coordinator, company.Id);
        return new World(company, admin, manager, coordinator);
    }

    private object Body(string password = "typed-password") => new
    {
        imapServer = "imap.example.com", imapPort = 993, emailUsername = "mailbox@example.com", password
    };

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    private static string[] States(JsonElement answer) => answer.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("state").GetString()!).ToArray();

    [Fact]
    public async Task Testing_typed_settings_reports_the_four_steps_tries_the_typed_password_and_saves_nothing()
    {
        var world = Seed();
        var probe = new FakeProbe();
        var admin = await SignedInAs(Host(probe), world.Admin.Email);

        var response = await admin.PostAsJsonAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config/test", Body());
        var answer = await Json(response);
        var stored = await Json(await admin.GetAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(answer.GetProperty("ok").GetBoolean());
        Assert.Equal(["ok", "ok", "ok", "ok"], States(answer));
        Assert.Equal(["reach", "sign_in", "read", "send"], answer.GetProperty("steps").EnumerateArray().Select(step => step.GetProperty("step").GetString()));
        Assert.Equal("typed-password", probe.Seen!.Password);
        Assert.Equal("imap.example.com", probe.Seen.ImapServer);
        Assert.Equal("smtp.example.com", probe.Seen.SmtpServer);
        Assert.Equal(587, probe.Seen.SmtpPort);
        Assert.False(stored.GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task A_failed_test_says_which_step_failed_what_happened_and_what_to_do_and_skips_the_rest()
    {
        var world = Seed();
        var admin = await SignedInAs(Host(new FakeProbe { FailAt = "sign_in" }), world.Admin.Email);

        var answer = await Json(await admin.PostAsJsonAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config/test", Body()));

        Assert.False(answer.GetProperty("ok").GetBoolean());
        Assert.Equal(["ok", "failed", "skipped", "skipped"], States(answer));
        var failed = answer.GetProperty("steps")[1];
        Assert.Equal("sign_in did not work.", failed.GetProperty("message").GetString());
        Assert.Equal("Try this.", failed.GetProperty("hint").GetString());
    }

    [Fact]
    public async Task Testing_needs_the_password_and_a_valid_port_like_saving_does()
    {
        var world = Seed();
        var probe = new FakeProbe();
        var admin = await SignedInAs(Host(probe), world.Admin.Email);

        var noPassword = await admin.PostAsJsonAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config/test", Body(password: ""));

        Assert.Equal(HttpStatusCode.BadRequest, noPassword.StatusCode);
        Assert.Equal(0, probe.Calls);
    }

    [Fact]
    public async Task Checking_the_saved_mailbox_uses_the_saved_password_and_remembers_a_failure_then_a_success()
    {
        var world = Seed();
        var probe = new FakeProbe { FailAt = "sign_in" };
        var host = Host(probe);
        var protector = host.Services.GetRequiredService<ISecretProtector>();
        api.SeedAs(db => db.CoordinatorEmailConfigs.Add(CoordinatorEmailConfig.Create(
            world.Coordinator.Id, world.Company.Id, "imap.example.com", 993, "mailbox@example.com", protector.Protect("saved-password"), true)));
        var admin = await SignedInAs(host, world.Admin.Email);

        var failed = await Json(await admin.PostAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config/check", null));
        var afterFailure = await Json(await admin.GetAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config"));
        probe.FailAt = null;
        var worked = await Json(await admin.PostAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config/check", null));
        var afterSuccess = await Json(await admin.GetAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config"));

        Assert.Equal("saved-password", probe.Seen!.Password);
        Assert.False(failed.GetProperty("ok").GetBoolean());
        Assert.Equal("problem", afterFailure.GetProperty("connectionStatus").GetString());
        Assert.Equal("sign_in did not work. Try this.", afterFailure.GetProperty("lastAuthError").GetString());
        Assert.NotEqual(JsonValueKind.Null, afterFailure.GetProperty("lastCheckedAt").ValueKind);
        Assert.True(worked.GetProperty("ok").GetBoolean());
        Assert.Equal("connected", afterSuccess.GetProperty("connectionStatus").GetString());
        Assert.Equal(JsonValueKind.Null, afterSuccess.GetProperty("lastAuthError").ValueKind);
    }

    [Fact]
    public async Task A_microsoft_mailbox_that_must_be_connected_again_fails_at_signing_in_without_trying_the_server()
    {
        var world = Seed();
        var probe = new FakeProbe();
        var config = CoordinatorEmailConfig.CreateMicrosoft(world.Coordinator.Id, world.Company.Id, "mailbox@example.com", "token", "tenant");
        config.MarkNeedsReconnect("Microsoft refused the saved sign-in.");
        api.SeedAs(db => db.CoordinatorEmailConfigs.Add(config));
        var admin = await SignedInAs(Host(probe), world.Admin.Email);

        var answer = await Json(await admin.PostAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config/check", null));
        var stored = await Json(await admin.GetAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config"));

        Assert.False(answer.GetProperty("ok").GetBoolean());
        Assert.Equal(["skipped", "failed", "skipped", "skipped"], States(answer));
        Assert.Equal(0, probe.Calls);
        Assert.Equal("needs_reconnect", stored.GetProperty("connectionStatus").GetString());
    }

    [Fact]
    public async Task Checking_a_coordinator_without_a_mailbox_is_not_found()
    {
        var world = Seed();
        var admin = await SignedInAs(Host(new FakeProbe()), world.Admin.Email);

        var response = await admin.PostAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config/check", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Only_the_company_admin_of_that_company_may_test_or_check()
    {
        var world = Seed();
        var other = Seed();
        var host = Host(new FakeProbe());
        foreach (var email in new[] { world.Manager.Email, world.Coordinator.Email })
        {
            var client = await SignedInAs(host, email);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config/test", Body())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config/check", null)).StatusCode);
        }
        var admin = await SignedInAs(host, other.Admin.Email);

        var crossTenant = await admin.PostAsJsonAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config/test", Body());

        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
    }

    private object VerifiedBody(string email = "mailbox@example.com") => new
    {
        imapServer = "imap.example.com", imapPort = 993, emailUsername = email, password = "typed-password", verify = true
    };

    [Fact]
    public async Task Saving_with_verify_refuses_a_mailbox_that_does_not_work_and_stores_nothing()
    {
        var world = Seed();
        var admin = await SignedInAs(Host(new FakeProbe { FailAt = "sign_in" }), world.Admin.Email);

        var response = await admin.PutAsJsonAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config", VerifiedBody());
        var problem = await Json(response);
        var stored = await Json(await admin.GetAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("mailbox_test_failed", problem.GetProperty("code").GetString());
        Assert.Contains("sign_in did not work.", problem.GetProperty("detail").GetString());
        Assert.Contains(problem.GetProperty("problems").EnumerateArray(), item => item.GetString()!.Contains("Try this."));
        Assert.False(stored.GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task Saving_with_verify_stores_a_working_mailbox_already_marked_as_checked()
    {
        var world = Seed();
        var admin = await SignedInAs(Host(new FakeProbe()), world.Admin.Email);

        var response = await admin.PutAsJsonAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config", VerifiedBody());
        var stored = await Json(await admin.GetAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(stored.GetProperty("configured").GetBoolean());
        Assert.Equal("connected", stored.GetProperty("connectionStatus").GetString());
        Assert.NotEqual(JsonValueKind.Null, stored.GetProperty("lastCheckedAt").ValueKind);
    }

    [Fact]
    public async Task Saving_without_verify_does_not_try_the_mailbox_as_before()
    {
        var world = Seed();
        var probe = new FakeProbe { FailAt = "sign_in" };
        var admin = await SignedInAs(Host(probe), world.Admin.Email);

        var response = await admin.PutAsJsonAsync($"/api/v1/company/users/{world.Coordinator.Id}/email-config", Body());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, probe.Calls);
    }

    [Fact]
    public async Task Creating_a_coordinator_with_verify_creates_nothing_when_the_mailbox_fails()
    {
        var world = Seed();
        var admin = await SignedInAs(Host(new FakeProbe { FailAt = "reach" }), world.Admin.Email);
        var email = $"verify-failed-{Interlocked.Increment(ref sequence)}@example.com";

        var response = await admin.PostAsJsonAsync("/api/v1/company/users", new
        {
            email, fullName = "Verify Failed", password = "StrongPassword123!", role = "coordinator", emailConfig = VerifiedBody()
        });
        var users = await admin.GetStringAsync("/api/v1/company/users");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("mailbox_test_failed", (await Json(response)).GetProperty("code").GetString());
        Assert.DoesNotContain(email, users);
    }

    [Fact]
    public async Task Creating_a_coordinator_with_verify_stores_the_mailbox_as_checked_when_it_works()
    {
        var world = Seed();
        var admin = await SignedInAs(Host(new FakeProbe()), world.Admin.Email);
        var email = $"verify-worked-{Interlocked.Increment(ref sequence)}@example.com";

        var created = await admin.PostAsJsonAsync("/api/v1/company/users", new
        {
            email, fullName = "Verify Worked", password = "StrongPassword123!", role = "coordinator", emailConfig = VerifiedBody(email)
        });
        var id = (await Json(created)).GetProperty("id").GetGuid();
        var stored = await Json(await admin.GetAsync($"/api/v1/company/users/{id}/email-config"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("connected", stored.GetProperty("connectionStatus").GetString());
        Assert.NotEqual(JsonValueKind.Null, stored.GetProperty("lastCheckedAt").ValueKind);
    }
}
