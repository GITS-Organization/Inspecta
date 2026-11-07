namespace Inspecta.Application.Interfaces;

/// <summary>What Microsoft handed out: an access token for IMAP and SMTP, and (when it rotates) a refresh token.</summary>
public sealed record MicrosoftTokens(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAt,
    string MailboxAddress,
    string MicrosoftTenantId);

/// <summary>Talks to the Microsoft sign-in service for the mailbox connection.</summary>
public interface IMicrosoftOAuthClient
{
    /// <summary>False until the client id, secret and redirect address are set.</summary>
    bool IsConfigured { get; }

    /// <summary>The Microsoft sign-in address for the coordinator browser (authorization code with PKCE).</summary>
    string BuildAuthorizationUrl(string state, string codeChallenge);

    /// <exception cref="Exceptions.MicrosoftTokenRejectedException">Microsoft refused the code.</exception>
    Task<MicrosoftTokens> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default);

    /// <exception cref="Exceptions.MicrosoftTokenRejectedException">Microsoft no longer accepts the refresh token.</exception>
    Task<MicrosoftTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}
