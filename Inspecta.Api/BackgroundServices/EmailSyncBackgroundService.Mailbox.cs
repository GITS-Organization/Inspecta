using System.Collections.Concurrent;
using System.Globalization;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Inspecta.Api.BackgroundServices;

public sealed partial class EmailSyncBackgroundService
{
    private const int MaxImportAttempts = 5;
    private static readonly ConcurrentDictionary<(Guid CoordinatorId, long Uid), int> FailedImports = new();

    private async Task SyncMailboxAsync(
        CoordinatorEmailConfig config,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var protector = services.GetRequiredService<ISecretProtector>();
            var repository = services.GetRequiredService<IEmailSyncRepository>();
            var password = protector.Unprotect(config.EncryptedPassword);

            using var client = new ImapClient();
            var secureSocketOptions = config.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;
            await ConnectSafelyAsync(client, config, secureSocketOptions, cancellationToken);
            await client.AuthenticateAsync(config.EmailUsername, password, cancellationToken);

            // Read-only: the mailbox is never modified. Progress is tracked by UID instead of the read flag.
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
            var uidValidity = inbox.UidValidity.ToString(CultureInfo.InvariantCulture);

            var syncFrom = config.SyncFromUtc;
            long cursor;
            if (config.LastUidValidity == uidValidity)
            {
                cursor = config.LastUid;
            }
            else
            {
                // First sync, or the server renumbered the folder: ignore everything that is already there.
                if (config.LastUidValidity is not null) syncFrom = DateTimeOffset.UtcNow;
                cursor = await FindBaselineUidAsync(inbox, syncFrom, cancellationToken);
                await repository.SaveSyncCursorAsync(config.CoordinatorId, uidValidity, cursor, syncFrom, cancellationToken);
            }

            var rules = await repository.ListSenderRulesAsync(config.TenantId, cancellationToken);
            var newUids = (await inbox.SearchAsync(
                    SearchQuery.Uids(new UniqueIdRange(new UniqueId((uint)(cursor + 1)), UniqueId.MaxValue)),
                    cancellationToken))
                .Where(uid => uid.Id > cursor)
                .OrderBy(uid => uid.Id)
                .ToList();

            var advanceTo = cursor;
            var blocked = false;
            try
            {
                foreach (var uid in newUids)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var message = await inbox.GetMessageAsync(uid, cancellationToken);
                        var incoming = ToIncomingEmail(message, uidValidity, uid.Id);
                        var rule = ClientMailRule.FindMatch(rules.Select(item => item.Rule), incoming.FromAddress);
                        var clientName = rule is null ? null : rules.First(item => item.Rule.Id == rule.Id).ClientName;
                        var request = InspectionRequest.CreateFromEmail(
                            config.TenantId,
                            config.CoordinatorId,
                            incoming,
                            triage: rule is null ? RequestTriage.Maybe : RequestTriage.Request,
                            clientId: clientName is null ? null : rule!.ClientId,
                            clientName: clientName);

                        await repository.SaveRequestIfNewAsync(request, cancellationToken);
                        FailedImports.TryRemove((config.CoordinatorId, uid.Id), out _);
                        if (!blocked) advanceTo = uid.Id;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception) when (exception is not (ImapProtocolException or IOException))
                    {
                        // One bad message must not block the ones after it. The cursor stays in front of it so it is
                        // retried next cycle (later messages that were imported are skipped by the unique key). After
                        // repeated failures it is given up on, so it cannot hold the mailbox back forever.
                        var attempts = FailedImports.AddOrUpdate((config.CoordinatorId, uid.Id), 1, (_, count) => count + 1);
                        if (attempts >= MaxImportAttempts)
                        {
                            logger.LogError(
                                exception,
                                "Giving up on message UID {Uid} for coordinator {CoordinatorId} after {Attempts} attempts.",
                                uid.Id,
                                config.CoordinatorId,
                                attempts);
                            FailedImports.TryRemove((config.CoordinatorId, uid.Id), out _);
                            if (!blocked) advanceTo = uid.Id;
                        }
                        else
                        {
                            logger.LogError(
                                exception,
                                "Could not import message UID {Uid} for coordinator {CoordinatorId} (attempt {Attempts}); will retry.",
                                uid.Id,
                                config.CoordinatorId,
                                attempts);
                            blocked = true;
                        }
                    }
                }
            }
            finally
            {
                if (advanceTo > cursor)
                    await repository.SaveSyncCursorAsync(
                        config.CoordinatorId, uidValidity, advanceTo, syncFrom, CancellationToken.None);
            }

            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to synchronize email for coordinator {CoordinatorId}; other mailboxes will continue.",
                config.CoordinatorId);
        }
    }

    /// <summary>Highest UID of a message that arrived before <paramref name="syncFrom"/>; only newer ones are imported.</summary>
    private static async Task<long> FindBaselineUidAsync(
        IMailFolder inbox,
        DateTimeOffset syncFrom,
        CancellationToken cancellationToken)
    {
        long baseline = 0;
        var all = await inbox.SearchAsync(SearchQuery.All, cancellationToken);
        foreach (var chunk in all.Chunk(500))
        {
            var summaries = await inbox.FetchAsync(
                chunk,
                MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate,
                cancellationToken);
            foreach (var summary in summaries)
            {
                if (summary.InternalDate is null || summary.InternalDate.Value < syncFrom)
                    baseline = Math.Max(baseline, summary.UniqueId.Id);
            }
        }

        return baseline;
    }
}
