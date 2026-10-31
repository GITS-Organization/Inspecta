using System.Net;
using System.Net.Sockets;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Inspecta.Infrastructure.Mail;

public sealed class MailKitSmtpEmailSender(ILogger<MailKitSmtpEmailSender> logger) : IEmailSender
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ConnectAttemptTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan OverallConnectTimeout = TimeSpan.FromSeconds(20);

    public async Task SendAsync(SmtpSettings smtp, OutgoingEmail email, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = BuildMessage(email);
            using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };
            await ConnectSafelyAsync(client, smtp, cancellationToken);
            await client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not send the email {MessageId} through {Server}.", email.MessageId, smtp.Server);
            throw new EmailDeliveryException(Describe(exception), exception);
        }
    }

    private static MimeMessage BuildMessage(OutgoingEmail email)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(email.FromName, email.FromAddress));
        message.To.Add(new MailboxAddress(email.ToName, email.ToAddress));
        foreach (var copy in email.Cc ?? [])
            message.Cc.Add(MailboxAddress.Parse(copy));
        message.Subject = email.Subject;
        message.MessageId = email.MessageId;
        if (!string.IsNullOrWhiteSpace(email.InReplyTo))
            message.InReplyTo = email.InReplyTo;
        foreach (var reference in (email.References ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            message.References.Add(reference);

        // Plain text always; the HTML version (when given) makes the links clickable in mail clients that prefer HTML.
        var body = new BodyBuilder { TextBody = email.BodyText, HtmlBody = email.BodyHtml };
        body.Attachments.Add(email.AttachmentFileName, email.AttachmentContent, ContentType.Parse(email.AttachmentFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? "application/zip" : "application/pdf"));
        foreach (var more in email.MoreAttachments ?? [])
            body.Attachments.Add(more.FileName, more.Content, ContentType.Parse(more.ContentType));
        message.Body = body.ToMessageBody();
        return message;
    }

    private static async Task ConnectSafelyAsync(SmtpClient client, SmtpSettings smtp, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(smtp.Server, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(PublicAddressGuard.IsNonPublic))
            throw new InvalidOperationException("SMTP server must resolve only to public IP addresses.");

        // A blocked port never answers, so the operating system would wait minutes. Give up quickly and report it.
        using var overall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overall.CancelAfter(OverallConnectTimeout);

        var options = smtp.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        Exception? lastConnectionError = null;
        foreach (var address in addresses.OrderBy(address => address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1))
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(overall.Token);
                attempt.CancelAfter(ConnectAttemptTimeout);
                await socket.ConnectAsync(new IPEndPoint(address, smtp.Port), attempt.Token);
                await client.ConnectAsync(socket, smtp.Server, smtp.Port, options, attempt.Token);
                return;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                socket.Dispose();
                lastConnectionError = new TimeoutException($"No answer from {address}:{smtp.Port}.");
                if (overall.IsCancellationRequested) break;
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

        throw new IOException($"Could not connect to SMTP server {smtp.Server}:{smtp.Port}.", lastConnectionError);
    }

    // Only fixed texts reach the user; the real exception is logged and kept as the inner exception.
    private static string Describe(Exception exception) => exception switch
    {
        AuthenticationException =>
            "The mail server rejected the mailbox login. Check the mailbox password and that the account may send through SMTP.",
        SmtpCommandException { ErrorCode: SmtpErrorCode.SenderNotAccepted } =>
            "The mail server refused the sender address.",
        SmtpCommandException { ErrorCode: SmtpErrorCode.RecipientNotAccepted } =>
            "The mail server refused the recipient address.",
        SmtpCommandException =>
            "The mail server refused the message.",
        IOException { InnerException: TimeoutException } =>
            "The mail server did not answer. Check the SMTP server and port, and that the hosting environment allows outgoing mail on that port.",
        SslHandshakeException =>
            "A secure connection to the mail server could not be established. Check the SMTP port and SSL setting.",
        ParseException =>
            "The sender or recipient address is not a valid email address.",
        InvalidOperationException { Message: var message } when message.Contains("public IP", StringComparison.Ordinal) =>
            "The SMTP server must resolve to a public address.",
        _ =>
            "The mail server could not be reached."
    };
}
