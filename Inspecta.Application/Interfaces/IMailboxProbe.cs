using Inspecta.Application.Contracts.Email;

namespace Inspecta.Application.Interfaces;

/// <summary>What to try: where to read, where to send, and what to sign in with (a password or a Microsoft access token, exactly one).</summary>
public sealed record MailboxProbeSettings(
    string ImapServer, int ImapPort, bool ImapUseSsl,
    string Username, string? Password, string? AccessToken,
    string SmtpServer, int SmtpPort, bool SmtpUseSsl);

/// <summary>Tries a mailbox the way the app uses it, without changing anything in it: reach the server, sign in, read the inbox, sign in to send.</summary>
public interface IMailboxProbe
{
    /// <summary>Always the four steps in order (<c>reach</c>, <c>sign_in</c>, <c>read</c>, <c>send</c>); the steps after a failed one are <c>skipped</c>.</summary>
    Task<IReadOnlyList<MailboxCheckStep>> ProbeAsync(MailboxProbeSettings settings, CancellationToken cancellationToken = default);
}
