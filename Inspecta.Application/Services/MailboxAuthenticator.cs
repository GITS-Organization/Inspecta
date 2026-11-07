using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class MailboxAuthenticator(
    ISecretProtector secretProtector,
    IMicrosoftOAuthClient microsoft,
    ICoordinatorEmailConfigRepository configRepository,
    MailboxTokenCache cache,
    TimeProvider? clock = null) : IMailboxAuthenticator
{
    private const int MaxAttempts = 3;
    private const string RefusedReason = "Microsoft no longer accepts the saved sign-in. Connect the mailbox again.";
    private readonly TimeProvider time = clock ?? TimeProvider.System;

    /// <remarks>
    /// The config handed in only says which mailbox this is: the sync reads it without tracking, possibly minutes before its turn.
    /// Everything else (mode, sign-in, status, address) is read from the stored row, so a mailbox reconnected, switched or removed
    /// in the meantime is used as it is now, and a change is only written when the row still holds the token that was used.
    /// </remarks>
    public async Task<MailboxCredential> GetAsync(CoordinatorEmailConfig config, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var stored = await configRepository.GetByCoordinatorNoTrackingAsync(config.CoordinatorId, cancellationToken)
                ?? throw new AuthException("email_config_not_found", "This coordinator has no mailbox set up.", 404);

            if (stored.AuthMode == MailboxAuthMode.Password)
                return new MailboxCredential(stored.EmailUsername, secretProtector.Unprotect(stored.EncryptedPassword), null);
            if (stored.ConnectionStatus == MailboxConnectionStatus.NeedsReconnect) throw ReconnectRequired();
            var used = stored.EncryptedRefreshToken;
            if (string.IsNullOrWhiteSpace(used)) throw ReconnectRequired();

            var fingerprint = FingerprintOf(used, stored.EmailUsername);
            if (cache.TryGet(stored.CoordinatorId, fingerprint, time.GetUtcNow(), out var cached))
                return new MailboxCredential(stored.EmailUsername, null, cached);

            var gate = cache.GateFor(stored.CoordinatorId);
            await gate.WaitAsync(cancellationToken);
            try
            {
                // Another caller may have renewed the token while this one waited.
                if (cache.TryGet(stored.CoordinatorId, fingerprint, time.GetUtcNow(), out cached))
                    return new MailboxCredential(stored.EmailUsername, null, cached);

                var refreshToken = secretProtector.Unprotect(used);
                MicrosoftTokens tokens;
                try
                {
                    tokens = await microsoft.RefreshAsync(refreshToken, cancellationToken);
                }
                catch (MicrosoftTokenRejectedException)
                {
                    cache.Remove(stored.CoordinatorId);
                    if (await configRepository.MarkNeedsReconnectAsync(stored.CoordinatorId, used, RefusedReason, CancellationToken.None))
                        throw ReconnectRequired();
                    continue; // the row changed while Microsoft was answering (a reconnect): start again with it
                }

                if (tokens.RefreshToken is { Length: > 0 } rotated && rotated != refreshToken)
                {
                    var encrypted = secretProtector.Protect(rotated);
                    // The row changed meanwhile: this answer belongs to the old sign-in, so it is dropped, not written over the new one.
                    if (!await configRepository.RotateRefreshTokenAsync(stored.CoordinatorId, used, encrypted, cancellationToken))
                        continue;
                    fingerprint = FingerprintOf(encrypted, stored.EmailUsername);
                }

                cache.Set(stored.CoordinatorId, fingerprint, tokens.AccessToken, tokens.ExpiresAt);
                return new MailboxCredential(stored.EmailUsername, null, tokens.AccessToken);
            }
            finally
            {
                gate.Release();
            }
        }

        throw new InvalidOperationException("The mailbox sign-in kept changing while it was being renewed. Try again.");
    }

    private static string FingerprintOf(string encryptedRefreshToken, string mailboxAddress) =>
        $"{encryptedRefreshToken}|{mailboxAddress.Trim().ToLowerInvariant()}";

    private static AuthException ReconnectRequired() => new(
        "mailbox_reconnect_required",
        "The Outlook mailbox must be connected again: Microsoft no longer accepts the saved sign-in.",
        409);
}
