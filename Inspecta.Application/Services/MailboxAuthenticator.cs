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
    private readonly TimeProvider time = clock ?? TimeProvider.System;

    public async Task<MailboxCredential> GetAsync(CoordinatorEmailConfig config, CancellationToken cancellationToken = default)
    {
        if (config.AuthMode == MailboxAuthMode.Password)
            return new MailboxCredential(config.EmailUsername, secretProtector.Unprotect(config.EncryptedPassword), null);

        if (config.ConnectionStatus == MailboxConnectionStatus.NeedsReconnect) throw ReconnectRequired();
        if (cache.TryGet(config.CoordinatorId, time.GetUtcNow(), out var cached))
            return new MailboxCredential(config.EmailUsername, null, cached);

        var gate = cache.GateFor(config.CoordinatorId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Another caller may have renewed the token while this one waited.
            if (cache.TryGet(config.CoordinatorId, time.GetUtcNow(), out cached))
                return new MailboxCredential(config.EmailUsername, null, cached);

            if (string.IsNullOrWhiteSpace(config.EncryptedRefreshToken)) throw ReconnectRequired();
            var refreshToken = secretProtector.Unprotect(config.EncryptedRefreshToken);
            MicrosoftTokens tokens;
            try
            {
                tokens = await microsoft.RefreshAsync(refreshToken, cancellationToken);
            }
            catch (MicrosoftTokenRejectedException)
            {
                cache.Remove(config.CoordinatorId);
                await PersistAsync(config, item => item.MarkNeedsReconnect("Microsoft no longer accepts the saved sign-in. Connect the mailbox again."), CancellationToken.None);
                throw ReconnectRequired();
            }

            if (tokens.RefreshToken is { Length: > 0 } rotated && rotated != refreshToken)
            {
                var encrypted = secretProtector.Protect(rotated);
                await PersistAsync(config, item => item.RotateRefreshToken(encrypted), cancellationToken);
            }

            cache.Set(config.CoordinatorId, tokens.AccessToken, tokens.ExpiresAt);
            return new MailboxCredential(config.EmailUsername, null, tokens.AccessToken);
        }
        finally
        {
            gate.Release();
        }
    }

    // The config handed in may be a read-only copy (the sync reads without tracking), and saving such a copy would insert
    // a second row: the change goes to the stored row, and to the copy so the caller sees it too.
    private async Task PersistAsync(CoordinatorEmailConfig config, Action<CoordinatorEmailConfig> change, CancellationToken cancellationToken)
    {
        change(config);
        var stored = await configRepository.GetByCoordinatorAsync(config.CoordinatorId, cancellationToken);
        if (stored is null) return;
        if (!ReferenceEquals(stored, config)) change(stored);
        await configRepository.SaveAsync(stored, cancellationToken);
    }

    private static AuthException ReconnectRequired() => new(
        "mailbox_reconnect_required",
        "The Outlook mailbox must be connected again: Microsoft no longer accepts the saved sign-in.",
        409);
}
