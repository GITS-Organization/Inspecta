using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

/// <summary>What to sign in to a mailbox with: a password, or a Microsoft access token (exactly one of them is set).</summary>
public sealed record MailboxCredential(string Username, string? Password, string? AccessToken);

public interface IMailboxAuthenticator
{
    /// <summary>The credential for IMAP or SMTP; a Microsoft mailbox gets a fresh access token, a password mailbox its decrypted password.</summary>
    /// <exception cref="Exceptions.AuthException"><c>mailbox_reconnect_required</c> (409): Microsoft no longer accepts the saved sign-in.</exception>
    Task<MailboxCredential> GetAsync(CoordinatorEmailConfig config, CancellationToken cancellationToken = default);
}
