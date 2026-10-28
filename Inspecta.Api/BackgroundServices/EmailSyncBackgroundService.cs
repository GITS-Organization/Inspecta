using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Inspecta.Domain.Entities;
using Inspecta.Application.Interfaces;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Inspecta.Api.BackgroundServices;

public sealed class EmailSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<EmailSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Clamp(configuration.GetValue("EmailSync:IntervalSeconds", 120), 30, 3600);
        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncAllAsync(stoppingToken);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<CoordinatorEmailConfig> configs;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IEmailSyncRepository>();
            configs = await repository.ListActiveConfigsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not load active coordinator email configurations.");
            return;
        }

        foreach (var config in configs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SyncMailboxAsync(config, cancellationToken);
        }
    }

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

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
            var uidValidity = inbox.UidValidity.ToString(CultureInfo.InvariantCulture);
            var unreadUids = await inbox.SearchAsync(SearchQuery.NotSeen, cancellationToken);

            foreach (var uid in unreadUids.OrderBy(value => value.Id))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var message = await inbox.GetMessageAsync(uid, cancellationToken);
                    var request = InspectionRequest.CreateFromEmail(
                        config.TenantId,
                        config.CoordinatorId,
                        ToIncomingEmail(message, uidValidity, uid.Id));

                    // Save first, mark read second: a failure in between re-imports as a no-op, never loses the message.
                    await repository.SaveRequestIfNewAsync(request, cancellationToken);
                    await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception) when (exception is not (ImapProtocolException or IOException))
                {
                    // One bad message must not block the ones after it. It stays unread and is retried next cycle.
                    logger.LogError(
                        exception,
                        "Could not import message UID {Uid} for coordinator {CoordinatorId}; continuing with the next message.",
                        uid.Id,
                        config.CoordinatorId);
                }
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

    private static IncomingEmail ToIncomingEmail(MimeKit.MimeMessage message, string uidValidity, uint uid)
    {
        var sender = message.From.Mailboxes.FirstOrDefault();
        var attachments = message.Attachments
            .Select(attachment => new EmailAttachmentInfo(
                attachment.ContentDisposition?.FileName ??
                attachment.ContentType.Name ??
                "attachment",
                attachment.ContentType.MimeType,
                attachment is MimeKit.MimePart part ? GetAttachmentSize(part) : 0))
            .ToArray();

        return new IncomingEmail(
            uidValidity,
            uid,
            message.TextBody,
            message.HtmlBody,
            sender?.Address,
            sender?.Name,
            message.Subject,
            ToEmailAddresses(message.To),
            ToEmailAddresses(message.Cc),
            ToEmailAddresses(message.Bcc),
            ToEmailAddresses(message.ReplyTo),
            attachments,
            message.MessageId,
            message.InReplyTo,
            message.References.Count > 0 ? string.Join(" ", message.References) : null,
            message.Date == DateTimeOffset.MinValue ? null : message.Date);
    }

    private static IReadOnlyList<EmailAddressInfo> ToEmailAddresses(MimeKit.InternetAddressList addresses) =>
        addresses.Mailboxes
            .Select(mailbox => new EmailAddressInfo(mailbox.Address, mailbox.Name))
            .ToArray();

    private static long GetAttachmentSize(MimeKit.MimePart attachment)
    {
        var stream = attachment.Content?.Stream;
        if (stream is null)
            return 0;

        try
        {
            return stream.Length;
        }
        catch (NotSupportedException)
        {
            return 0;
        }
    }

    private static async Task ConnectSafelyAsync(
        ImapClient client,
        CoordinatorEmailConfig config,
        SecureSocketOptions secureSocketOptions,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(config.ImapServer, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(IsNonPublicAddress))
            throw new InvalidOperationException("IMAP server must resolve only to public IP addresses.");

        Exception? lastConnectionError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, config.ImapPort), cancellationToken);
                await client.ConnectAsync(
                    socket,
                    config.ImapServer,
                    config.ImapPort,
                    secureSocketOptions,
                    cancellationToken);
                return;
            }
            catch (Exception exception) when (exception is SocketException or IOException)
            {
                socket.Dispose();
                lastConnectionError = exception;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new IOException(
            $"Could not connect to IMAP server {config.ImapServer}:{config.ImapPort}.",
            lastConnectionError);
    }

    private static bool IsNonPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) || address.IsIPv6LinkLocal ||
            address.IsIPv6Multicast || address.IsIPv6SiteLocal)
            return true;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 0 ||
                   bytes[0] == 10 ||
                   bytes[0] == 127 ||
                   bytes[0] >= 224 ||
                   bytes[0] == 169 && bytes[1] == 254 ||
                   bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
                   bytes[0] == 192 && bytes[1] == 168 ||
                   bytes[0] == 100 && bytes[1] is >= 64 and <= 127 ||
                   bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0 ||
                   bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2 ||
                   bytes[0] == 198 && bytes[1] is 18 or 19 ||
                   bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100 ||
                   bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return (bytes[0] & 0xFE) == 0xFC ||
                   bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80 ||
                   (bytes[0] & 0xE0) != 0x20 ||
                   bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8;
        }

        return true;
    }
}
