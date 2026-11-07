namespace Inspecta.Application.Interfaces;

/// <summary>Outgoing mail server and the mailbox login used to send through it: the password, or a Microsoft access token (OAuth) instead.</summary>
public sealed record SmtpSettings(string Server, int Port, bool UseSsl, string Username, string Password, string? AccessToken = null);

/// <summary>A file attached to an outgoing email.</summary>
public sealed record EmailAttachment(string FileName, byte[] Content, string ContentType);

/// <summary>A message with a PDF attachment (and optionally more files), optionally part of an existing email thread.</summary>
public sealed record OutgoingEmail(
    string FromAddress,
    string? FromName,
    string ToAddress,
    string? ToName,
    string Subject,
    string BodyText,
    string MessageId,
    string? InReplyTo,
    string? References,
    string AttachmentFileName,
    byte[] AttachmentContent,
    IReadOnlyList<EmailAttachment>? MoreAttachments = null,
    IReadOnlyList<string>? Cc = null,
    string? BodyHtml = null,
    /// <summary>Images the HTML shows with <c>src="cid:..."</c>; the file name is the content id.</summary>
    IReadOnlyList<EmailAttachment>? InlineImages = null);

public interface IEmailSender
{
    /// <exception cref="Exceptions.EmailDeliveryException">The mail server could not be reached or refused the message.</exception>
    Task SendAsync(SmtpSettings smtp, OutgoingEmail email, CancellationToken cancellationToken = default);
}
