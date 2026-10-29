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
        message.Subject = email.Subject;
        message.MessageId = email.MessageId;
        if (!string.IsNullOrWhiteSpace(email.InReplyTo))
            message.InReplyTo = email.InReplyTo;
        foreach (var reference in (email.References ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            message.References.Add(reference);

        var body = new BodyBuilder { TextBody = email.BodyText };
        body.Attachments.Add(email.AttachmentFileName, email.AttachmentContent, ContentType.Parse("application/pdf"));
        message.Body = body.ToMessageBody();
        return message;
    }

    private static async Task ConnectSafelyAsync(SmtpClient client, SmtpSettings smtp, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(smtp.Server, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(PublicAddressGuard.IsNonPublic))
            throw new InvalidOperationException("SMTP server must resolve only to public IP addresses.");

        var options = smtp.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        Exception? lastConnectionError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, smtp.Port), cancellationToken);
                await client.ConnectAsync(socket, smtp.Server, smtp.Port, options, cancellationToken);
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
