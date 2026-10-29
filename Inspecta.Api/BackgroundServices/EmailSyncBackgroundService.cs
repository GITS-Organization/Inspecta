using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Mail;
using Inspecta.Application.Interfaces;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Inspecta.Api.BackgroundServices;

public sealed partial class EmailSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<EmailSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Clamp(configuration.GetValue("EmailSync:IntervalSeconds", 120), 10, 3600);
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
        if (addresses.Length == 0 || addresses.Any(PublicAddressGuard.IsNonPublic))
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
}
