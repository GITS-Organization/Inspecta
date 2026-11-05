using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Inspecta.Domain.Tests;

/// <summary>Signing in again without a password: a refresh token works once, and a token used twice shuts the whole sign-in down.</summary>
public sealed class RefreshTokenHttpTests : IClassFixture<ApiHttpTests.Api>
{
    private const string Password = "Correct-Horse-Battery-1";
    private static readonly object SeedLock = new();
    private readonly ApiHttpTests.Api api;

    public RefreshTokenHttpTests(ApiHttpTests.Api api) => this.api = api;

    private HttpClient Http()
    {
        lock (SeedLock) if (api.ManagerA == Guid.Empty) api.Seed();
        return api.CreateClient();
    }

    private static async Task<(string Access, string Refresh)> SignInAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "manager-a@example.com", password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Tokens(await response.Content.ReadAsStringAsync());
    }

    private static (string Access, string Refresh) Tokens(string json)
    {
        using var document = JsonDocument.Parse(json);
        return (document.RootElement.GetProperty("accessToken").GetString()!, document.RootElement.GetProperty("refreshToken").GetString()!);
    }

    private static Task<HttpResponseMessage> Refresh(HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });

    private static async Task<string> CodeOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString()! : string.Empty;
    }

    [Fact]
    public async Task ARefreshTokenGivesNewTokensAndTheNewAccessTokenWorks()
    {
        var client = Http();
        var (_, refresh) = await SignInAsync(client);

        var response = await Refresh(client, refresh);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (access, next) = Tokens(await response.Content.ReadAsStringAsync());
        Assert.NotEqual(refresh, next);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task UsingAnOldRefreshTokenAgainIsRefusedAndAlsoClosesTheNewOne()
    {
        var client = Http();
        var (_, first) = await SignInAsync(client);
        var (_, second) = Tokens(await (await Refresh(client, first)).Content.ReadAsStringAsync());

        var reused = await Refresh(client, first);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Equal("refresh_token_reused", await CodeOf(reused));

        // Somebody holds a stolen copy: the whole chain is closed, so the newest token stops working too.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, second)).StatusCode);
    }

    [Fact]
    public async Task AnUnknownRefreshTokenIsRefused()
    {
        var client = Http();

        var response = await Refresh(client, "not-a-token-this-server-ever-issued");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", await CodeOf(response));
    }

    [Fact]
    public async Task SigningOutClosesTheRefreshToken()
    {
        var client = Http();
        var (access, refresh) = await SignInAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = refresh });
        Assert.True(logout.IsSuccessStatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, refresh)).StatusCode);
    }
}
