using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Web;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Inspecta.Domain.Tests;

/// <summary>A coordinator connecting an Outlook mailbox, over HTTP, with a fake Microsoft in place of the real sign-in service.</summary>
public sealed class MicrosoftConnectionHttpTests : IClassFixture<ApiHttpTests.Api>
{
    private const string Callback = "/api/v1/email-connection/microsoft/callback";
    private static readonly string Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static int caller = 100;
    private static int accounts;
    private readonly ApiHttpTests.Api api;

    public MicrosoftConnectionHttpTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private sealed class FakeMicrosoft : IMicrosoftOAuthClient
    {
        public string Mailbox { get; set; } = "ahmed@example.com";
        public bool RejectCode { get; set; }
        public bool IsConfigured => true;

        public string BuildAuthorizationUrl(string state, string codeChallenge) =>
            $"https://login.example.test/authorize?state={Uri.EscapeDataString(state)}&code_challenge={Uri.EscapeDataString(codeChallenge)}";

        public Task<MicrosoftTokens> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
        {
            if (RejectCode) throw new MicrosoftTokenRejectedException("rejected");
            return Task.FromResult(new MicrosoftTokens("access-secret-1", "refresh-secret-1", DateTimeOffset.UtcNow.AddHours(1), Mailbox, "tenant-77"));
        }

        public Task<MicrosoftTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private WebApplicationFactory<Program> Host(FakeMicrosoft? microsoft = null, bool allowAnyDomain = false) =>
        api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("IMAP_ENCRYPTION_KEY", Key);
            if (microsoft is null) return;
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(value => value.ServiceType == typeof(IMicrosoftOAuthClient)
                                                                  || value.ServiceType == typeof(MicrosoftOAuthOptions)).ToList())
                    services.Remove(descriptor);
                services.AddSingleton<IMicrosoftOAuthClient>(microsoft);
                services.AddSingleton(new MicrosoftOAuthOptions { AllowAnyDomain = allowAnyDomain });
            });
        });

    private static HttpClient Client(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.40." + Interlocked.Increment(ref caller));
        return client;
    }

    private static async Task<string> SignIn(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static HttpClient As(HttpClient client, string access)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return client;
    }

    private AppUser NewCoordinator() => api.AddUser($"ms-coordinator-{Interlocked.Increment(ref accounts)}@example.com", UserRole.Coordinator, api.ManagerA);

    private static async Task<string> StartAsync(HttpClient coordinator)
    {
        var response = await coordinator.PostAsync("/api/v1/me/email-connection/microsoft/start", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var url = new Uri(json.RootElement.GetProperty("authorizationUrl").GetString()!);
        return HttpUtility.ParseQueryString(url.Query)["state"]!;
    }

    private static Task<HttpResponseMessage> CallBack(HttpClient anonymous, string state, string query = "code=code-1") =>
        anonymous.GetAsync($"{Callback}?{query}&state={Uri.EscapeDataString(state)}");

    private static async Task<JsonElement> ConfigOf(HttpClient manager, Guid coordinatorId)
    {
        var response = await manager.GetAsync($"/api/v1/manager/team/{coordinatorId}/email-config");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private async Task<HttpClient> ManagerAsync(WebApplicationFactory<Program> host, string email = "company-admin-a@example.com") =>
        As(Client(host), await SignIn(Client(host), email));

    [Fact]
    public async Task StartReturnsTheMicrosoftUrlForACoordinator()
    {
        using var host = Host(new FakeMicrosoft());
        var user = NewCoordinator();
        var coordinator = As(Client(host), await SignIn(Client(host), user.Email));

        var response = await coordinator.PostAsync("/api/v1/me/email-connection/microsoft/start", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var url = new Uri(json.RootElement.GetProperty("authorizationUrl").GetString()!);
        var query = HttpUtility.ParseQueryString(url.Query);
        Assert.Equal("login.example.test", url.Host);
        Assert.False(string.IsNullOrEmpty(query["state"]));
        Assert.False(string.IsNullOrEmpty(query["code_challenge"]));
    }

    [Fact]
    public async Task StartIsRefusedForATechnicalManagerAndWithoutSignIn()
    {
        using var host = Host(new FakeMicrosoft());

        var anonymous = await Client(host).PostAsync("/api/v1/me/email-connection/microsoft/start", null);
        var manager = await (await ManagerAsync(host)).PostAsync("/api/v1/me/email-connection/microsoft/start", null);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, manager.StatusCode);
    }

    [Fact]
    public async Task StartWithoutMicrosoftSettingsAnswers503MicrosoftNotConfigured()
    {
        using var host = Host();
        var user = NewCoordinator();
        var coordinator = As(Client(host), await SignIn(Client(host), user.Email));

        var response = await coordinator.PostAsync("/api/v1/me/email-connection/microsoft/start", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("microsoft_not_configured", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CallbackSavesAMicrosoftConfigWithAnEncryptedRefreshToken()
    {
        using var host = Host(new FakeMicrosoft());
        var user = NewCoordinator();
        var coordinator = As(Client(host), await SignIn(Client(host), user.Email));
        var manager = await ManagerAsync(host);

        var state = await StartAsync(coordinator);
        var page = await CallBack(Client(host), state);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("ahmed@example.com", await page.Content.ReadAsStringAsync());
        var config = await ConfigOf(manager, user.Id);
        Assert.True(config.GetProperty("configured").GetBoolean());
        Assert.Equal("microsoft_oauth", config.GetProperty("authMode").GetString());
        Assert.Equal("connected", config.GetProperty("connectionStatus").GetString());
        Assert.Equal("ahmed@example.com", config.GetProperty("emailUsername").GetString());
        Assert.DoesNotContain("refresh-secret-1", config.ToString());
        Assert.DoesNotContain("access-secret-1", config.ToString());

        using var scope = host.Services.CreateScope();
        var stored = scope.ServiceProvider.GetRequiredService<InspectaDbContext>().CoordinatorEmailConfigs
            .Single(item => item.CoordinatorId == user.Id);
        Assert.NotEqual("refresh-secret-1", stored.EncryptedRefreshToken);
        Assert.Equal("refresh-secret-1", scope.ServiceProvider.GetRequiredService<ISecretProtector>().Unprotect(stored.EncryptedRefreshToken!));
    }

    [Fact]
    public async Task CallbackWithATamperedStateIsRefused()
    {
        using var host = Host(new FakeMicrosoft());
        var user = NewCoordinator();
        var coordinator = As(Client(host), await SignIn(Client(host), user.Email));
        var state = await StartAsync(coordinator);
        var tampered = state[..^2] + (state[^2] == 'A' ? "B" : "A") + state[^1];

        var page = await CallBack(Client(host), tampered);

        Assert.Equal(HttpStatusCode.BadRequest, page.StatusCode);
        Assert.Contains("invalid_oauth_state", await page.Content.ReadAsStringAsync());
        Assert.False((await ConfigOf(await ManagerAsync(host), user.Id)).GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task CallbackWithAnExpiredStateIsRefused()
    {
        using var host = Host(new FakeMicrosoft());
        var user = NewCoordinator();
        var expired = host.Services.GetRequiredService<ISecretProtector>().Protect(JsonSerializer.Serialize(
            new MicrosoftConnectionState(user.Id, api.ManagerA, "verifier-1", DateTimeOffset.UtcNow.AddMinutes(-1))));

        var page = await CallBack(Client(host), expired);

        Assert.Equal(HttpStatusCode.BadRequest, page.StatusCode);
        Assert.Contains("invalid_oauth_state", await page.Content.ReadAsStringAsync());
        Assert.False((await ConfigOf(await ManagerAsync(host), user.Id)).GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task ACodeMicrosoftRefusesChangesNothing()
    {
        var microsoft = new FakeMicrosoft { RejectCode = true };
        using var host = Host(microsoft);
        var user = NewCoordinator();
        var coordinator = As(Client(host), await SignIn(Client(host), user.Email));
        var state = await StartAsync(coordinator);

        var page = await CallBack(Client(host), state);

        Assert.Equal(HttpStatusCode.BadRequest, page.StatusCode);
        Assert.Contains("microsoft_sign_in_failed", await page.Content.ReadAsStringAsync());
        Assert.False((await ConfigOf(await ManagerAsync(host), user.Id)).GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task CallbackWithAMailboxOnAnotherDomainIsRefused()
    {
        using var host = Host(new FakeMicrosoft { Mailbox = "someone@gmail.com" });
        var user = NewCoordinator();
        var coordinator = As(Client(host), await SignIn(Client(host), user.Email));
        var state = await StartAsync(coordinator);

        var page = await CallBack(Client(host), state);

        Assert.Equal(HttpStatusCode.BadRequest, page.StatusCode);
        Assert.Contains("mailbox_domain_mismatch", await page.Content.ReadAsStringAsync());
        Assert.False((await ConfigOf(await ManagerAsync(host), user.Id)).GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task CallbackAllowsAnyDomainWhenConfigured()
    {
        using var host = Host(new FakeMicrosoft { Mailbox = "tester@contoso.onmicrosoft.com" }, allowAnyDomain: true);
        var user = NewCoordinator();
        var coordinator = As(Client(host), await SignIn(Client(host), user.Email));
        var state = await StartAsync(coordinator);

        var page = await CallBack(Client(host), state);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.True((await ConfigOf(await ManagerAsync(host), user.Id)).GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task CallbackWhenTheUserRefusedConsentShowsAMessageAndSavesNothing()
    {
        using var host = Host(new FakeMicrosoft());
        var user = NewCoordinator();
        var coordinator = As(Client(host), await SignIn(Client(host), user.Email));
        var state = await StartAsync(coordinator);

        var page = await Client(host).GetAsync($"{Callback}?error=access_denied&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.BadRequest, page.StatusCode);
        Assert.Contains("microsoft_sign_in_failed", await page.Content.ReadAsStringAsync());
        Assert.False((await ConfigOf(await ManagerAsync(host), user.Id)).GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task ACoordinatorSeesTheirOwnConnectionStatus()
    {
        using var host = Host(new FakeMicrosoft());
        var user = NewCoordinator();
        var coordinator = As(Client(host), await SignIn(Client(host), user.Email));

        var before = await coordinator.GetAsync("/api/v1/me/email-connection");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.False(JsonDocument.Parse(await before.Content.ReadAsStringAsync()).RootElement.GetProperty("configured").GetBoolean());

        await CallBack(Client(host), await StartAsync(coordinator));

        var after = JsonDocument.Parse(await (await coordinator.GetAsync("/api/v1/me/email-connection")).Content.ReadAsStringAsync()).RootElement;
        Assert.True(after.GetProperty("configured").GetBoolean());
        Assert.Equal("microsoft_oauth", after.GetProperty("authMode").GetString());
        Assert.Equal("connected", after.GetProperty("connectionStatus").GetString());
        Assert.Equal("ahmed@example.com", after.GetProperty("emailUsername").GetString());
    }

    [Fact]
    public async Task OnlyACoordinatorCanReadTheirOwnConnectionStatus()
    {
        using var host = Host(new FakeMicrosoft());

        var anonymous = await Client(host).GetAsync("/api/v1/me/email-connection");
        var manager = await (await ManagerAsync(host)).GetAsync("/api/v1/me/email-connection");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, manager.StatusCode);
    }

    [Fact]
    public async Task ADisconnectByTheManagerRemovesTheConfig()
    {
        using var host = Host(new FakeMicrosoft());
        var user = NewCoordinator();
        var coordinator = As(Client(host), await SignIn(Client(host), user.Email));
        await CallBack(Client(host), await StartAsync(coordinator));
        var manager = await ManagerAsync(host);

        var removed = await manager.DeleteAsync($"/api/v1/manager/team/{user.Id}/email-config");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.False((await ConfigOf(manager, user.Id)).GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task AnotherTeamsManagerGets404OnDisconnect()
    {
        using var host = Host(new FakeMicrosoft());
        var user = NewCoordinator();
        var coordinator = As(Client(host), await SignIn(Client(host), user.Email));
        await CallBack(Client(host), await StartAsync(coordinator));
        var other = await ManagerAsync(host, "company-admin-b@example.com");

        var response = await other.DeleteAsync($"/api/v1/manager/team/{user.Id}/email-config");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True((await ConfigOf(await ManagerAsync(host), user.Id)).GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task ReconnectingAfterNeedsReconnectRestoresConnected()
    {
        using var host = Host(new FakeMicrosoft());
        var user = NewCoordinator();
        var coordinator = As(Client(host), await SignIn(Client(host), user.Email));
        await CallBack(Client(host), await StartAsync(coordinator));
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InspectaDbContext>();
            db.CoordinatorEmailConfigs.Single(item => item.CoordinatorId == user.Id).MarkNeedsReconnect("revoked");
            db.SaveChanges();
        }

        var manager = await ManagerAsync(host);
        var broken = await ConfigOf(manager, user.Id);
        Assert.Equal("needs_reconnect", broken.GetProperty("connectionStatus").GetString());
        Assert.Equal("revoked", broken.GetProperty("lastAuthError").GetString());

        await CallBack(Client(host), await StartAsync(coordinator));

        var fixedConfig = await ConfigOf(manager, user.Id);
        Assert.Equal("connected", fixedConfig.GetProperty("connectionStatus").GetString());
        Assert.Equal(JsonValueKind.Null, fixedConfig.GetProperty("lastAuthError").ValueKind);
    }
}
