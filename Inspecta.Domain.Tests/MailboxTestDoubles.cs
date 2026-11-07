using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Domain.Tests;

/// <summary>Hands out a fixed Microsoft access token, or refuses like a mailbox that must be connected again.</summary>
internal sealed class StubMailboxAuthenticator(string accessToken = "access-token-1") : IMailboxAuthenticator
{
    public bool Reject { get; set; }

    public Task<MailboxCredential> GetAsync(CoordinatorEmailConfig config, CancellationToken cancellationToken = default)
    {
        if (Reject)
            throw new AuthException("mailbox_reconnect_required", "The Outlook mailbox must be connected again.", 409);
        return Task.FromResult(new MailboxCredential(config.EmailUsername, null, accessToken));
    }
}
