using System.Net;
using System.Text;
using System.Web;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Services;
using Inspecta.Infrastructure.Mail;

namespace Inspecta.Domain.Tests;

public sealed class MicrosoftOAuthClientTests
{
    private const string Redirect = "https://api.example.test/api/v1/email-connection/microsoft/callback";
    private const string Scope =
        "https://outlook.office.com/IMAP.AccessAsUser.All https://outlook.office.com/SMTP.Send offline_access openid email profile";

    private sealed class StubHandler(Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(Uri? Uri, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request.RequestUri, body));
            return respond(body);
        }
    }

    private static MicrosoftOAuthOptions Options() => new() { ClientId = "client-1", ClientSecret = "secret-1", RedirectUri = Redirect };

    private static MicrosoftOAuthClient Client(StubHandler handler, MicrosoftOAuthOptions? options = null) =>
        new(new HttpClient(handler), options ?? Options());

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string Base64Url(string text) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string IdToken(string email, string tenant) =>
        $"{Base64Url("{\"alg\":\"none\"}")}.{Base64Url($"{{\"preferred_username\":\"{email}\",\"tid\":\"{tenant}\"}}")}.sig";

    [Fact]
    public void AuthorizationUrlCarriesTheScopesPkceAndState()
    {
        var url = new Uri(Client(new StubHandler(_ => Json(HttpStatusCode.OK, "{}"))).BuildAuthorizationUrl("state with.dots/+=", "challenge-1"));
        var query = HttpUtility.ParseQueryString(url.Query);

        Assert.Equal("login.microsoftonline.com", url.Host);
        Assert.Equal("/common/oauth2/v2.0/authorize", url.AbsolutePath);
        Assert.Equal("client-1", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal(Redirect, query["redirect_uri"]);
        Assert.Equal("query", query["response_mode"]);
        Assert.Equal(Scope, query["scope"]);
        Assert.Equal("state with.dots/+=", query["state"]);
        Assert.Equal("challenge-1", query["code_challenge"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("select_account", query["prompt"]);
    }

    [Fact]
    public async Task ExchangeCodeReadsTokensAndTheMailboxFromTheIdToken()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            $"{{\"access_token\":\"at-1\",\"refresh_token\":\"rt-1\",\"expires_in\":3600,\"id_token\":\"{IdToken("ahmed@integrity.example", "tenant-9")}\"}}"));

        var tokens = await Client(handler).ExchangeCodeAsync("code-1", "verifier-1");

        Assert.Equal("at-1", tokens.AccessToken);
        Assert.Equal("rt-1", tokens.RefreshToken);
        Assert.Equal("ahmed@integrity.example", tokens.MailboxAddress);
        Assert.Equal("tenant-9", tokens.MicrosoftTenantId);
        var remaining = tokens.ExpiresAt - DateTimeOffset.UtcNow;
        Assert.InRange(remaining.TotalSeconds, 3500, 3600);
        var call = Assert.Single(handler.Calls);
        Assert.Equal("/common/oauth2/v2.0/token", call.Uri!.AbsolutePath);
        var form = HttpUtility.ParseQueryString(call.Body);
        Assert.Equal("authorization_code", form["grant_type"]);
        Assert.Equal("code-1", form["code"]);
        Assert.Equal("verifier-1", form["code_verifier"]);
        Assert.Equal("client-1", form["client_id"]);
        Assert.Equal("secret-1", form["client_secret"]);
        Assert.Equal(Redirect, form["redirect_uri"]);
    }

    [Fact]
    public async Task RefreshReturnsTheRotatedRefreshToken()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "{\"access_token\":\"at-2\",\"refresh_token\":\"rt-2\",\"expires_in\":3599}"));

        var tokens = await Client(handler).RefreshAsync("rt-1");

        Assert.Equal("at-2", tokens.AccessToken);
        Assert.Equal("rt-2", tokens.RefreshToken);
        Assert.Equal(string.Empty, tokens.MailboxAddress);
        var form = HttpUtility.ParseQueryString(Assert.Single(handler.Calls).Body);
        Assert.Equal("refresh_token", form["grant_type"]);
        Assert.Equal("rt-1", form["refresh_token"]);
        Assert.Equal(Scope, form["scope"]);
    }

    [Fact]
    public async Task InvalidGrantThrowsMicrosoftTokenRejectedWithoutMicrosoftsText()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest,
            "{\"error\":\"invalid_grant\",\"error_description\":\"AADSTS70000: private detail\"}"));

        var exception = await Assert.ThrowsAsync<MicrosoftTokenRejectedException>(() => Client(handler).RefreshAsync("rt-1"));

        Assert.DoesNotContain("private detail", exception.Message);
    }

    [Fact]
    public async Task OtherFailuresThrowHttpRequestExceptionWithoutTheResponseBody()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.InternalServerError, "{\"secret\":\"boom-body-xyz\"}"));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => Client(handler).ExchangeCodeAsync("code-1", "verifier-1"));

        Assert.DoesNotContain("boom-body-xyz", exception.Message);
    }

    [Theory]
    [InlineData(null, "secret", Redirect)]
    [InlineData("client", null, Redirect)]
    [InlineData("client", "secret", null)]
    public void IsNotConfiguredWithoutClientIdSecretOrRedirect(string? clientId, string? secret, string? redirect)
    {
        var options = new MicrosoftOAuthOptions { ClientId = clientId, ClientSecret = secret, RedirectUri = redirect };

        Assert.False(Client(new StubHandler(_ => Json(HttpStatusCode.OK, "{}")), options).IsConfigured);
        Assert.True(Client(new StubHandler(_ => Json(HttpStatusCode.OK, "{}"))).IsConfigured);
    }
}
