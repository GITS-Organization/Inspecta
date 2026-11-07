using System.Text;
using System.Text.Json;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;

namespace Inspecta.Infrastructure.Mail;

public sealed class MicrosoftOAuthClient(HttpClient http, MicrosoftOAuthOptions options) : IMicrosoftOAuthClient
{
    // One access token serves both IMAP and SMTP: both belong to the same Outlook resource.
    public const string Scopes =
        "https://outlook.office.com/IMAP.AccessAsUser.All https://outlook.office.com/SMTP.Send offline_access openid email profile";

    private string Authority => options.Authority.TrimEnd('/');

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(options.ClientId) &&
        !string.IsNullOrWhiteSpace(options.ClientSecret) &&
        !string.IsNullOrWhiteSpace(options.RedirectUri);

    public string BuildAuthorizationUrl(string state, string codeChallenge)
    {
        var query = new (string Key, string Value)[]
        {
            ("client_id", options.ClientId ?? string.Empty),
            ("response_type", "code"),
            ("redirect_uri", options.RedirectUri ?? string.Empty),
            ("response_mode", "query"),
            ("scope", Scopes),
            ("state", state),
            ("code_challenge", codeChallenge),
            ("code_challenge_method", "S256"),
            ("prompt", "select_account")
        };
        return $"{Authority}/oauth2/v2.0/authorize?" +
               string.Join('&', query.Select(item => $"{item.Key}={Uri.EscapeDataString(item.Value)}"));
    }

    public Task<MicrosoftTokens> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default) =>
        RequestAsync(
        [
            ("grant_type", "authorization_code"),
            ("code", code),
            ("code_verifier", codeVerifier),
            ("redirect_uri", options.RedirectUri ?? string.Empty)
        ], cancellationToken);

    public Task<MicrosoftTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        RequestAsync(
        [
            ("grant_type", "refresh_token"),
            ("refresh_token", refreshToken),
            ("scope", Scopes)
        ], cancellationToken);

    private async Task<MicrosoftTokens> RequestAsync(IEnumerable<(string Key, string Value)> fields, CancellationToken cancellationToken)
    {
        var form = fields
            .Append(("client_id", options.ClientId ?? string.Empty))
            .Append(("client_secret", options.ClientSecret ?? string.Empty))
            .Select(item => new KeyValuePair<string, string>(item.Item1, item.Item2));
        using var response = await http.PostAsync($"{Authority}/oauth2/v2.0/token", new FormUrlEncodedContent(form), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // The text Microsoft sends is never passed on: it can name accounts and policies.
            if (ErrorCode(body) == "invalid_grant")
                throw new MicrosoftTokenRejectedException("Microsoft rejected the saved sign-in.");
            throw new HttpRequestException($"The Microsoft sign-in service answered {(int)response.StatusCode}.", null, response.StatusCode);
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var accessToken = Text(root, "access_token");
        if (string.IsNullOrEmpty(accessToken))
            throw new HttpRequestException("The Microsoft sign-in service did not return an access token.");
        var expiresIn = root.TryGetProperty("expires_in", out var seconds) && seconds.TryGetInt32(out var value) ? value : 3600;
        var (mailbox, tenant) = ReadIdToken(Text(root, "id_token"));
        return new MicrosoftTokens(accessToken, Text(root, "refresh_token"), DateTimeOffset.UtcNow.AddSeconds(expiresIn), mailbox, tenant);
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? ErrorCode(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return Text(document.RootElement, "error");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // The id token comes straight from Microsoft over TLS in answer to our own request with the client secret,
    // so reading its claims needs no signature check (OpenID Connect Core, section 3.1.3.7).
    private static (string Mailbox, string Tenant) ReadIdToken(string? idToken)
    {
        var parts = idToken?.Split('.');
        if (parts is not { Length: >= 2 }) return (string.Empty, string.Empty);
        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            var claims = document.RootElement;
            return (Text(claims, "email") ?? Text(claims, "preferred_username") ?? string.Empty, Text(claims, "tid") ?? string.Empty);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return (string.Empty, string.Empty);
        }
    }
}
