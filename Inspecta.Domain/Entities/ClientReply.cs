using System.Text.Json;

namespace Inspecta.Domain.Entities;

/// <summary>
/// An email the client sent back as an answer to a quotation we emailed (it replies to that email). It is kept with the request instead
/// of becoming a new request, and is cleared when the next offer goes out.
/// </summary>
public sealed class ClientReply
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ClientReply() { }

    public Guid Id { get; private set; }
    public Guid ManagerId { get; private set; }
    public Guid RequestId { get; private set; }

    /// <summary>The quotation whose email the client answered, when the reply says which one.</summary>
    public Guid? QuoteId { get; private set; }

    public string? MessageId { get; private set; }
    public string? InReplyTo { get; private set; }
    public string? FromAddress { get; private set; }
    public string? FromName { get; private set; }
    public string? Subject { get; private set; }
    public string? TextBody { get; private set; }
    public string? HtmlBody { get; private set; }
    public string? AttachmentMetadataJson { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }

    public static ClientReply Create(Guid managerId, Guid requestId, Guid? quoteId, IncomingEmail email, DateTimeOffset receivedAt)
    {
        if (managerId == Guid.Empty || requestId == Guid.Empty) throw new ArgumentException("Team and request are required.");
        ArgumentNullException.ThrowIfNull(email);

        var attachments = email.Attachments.ToArray();
        return new ClientReply
        {
            Id = Guid.NewGuid(),
            ManagerId = managerId,
            RequestId = requestId,
            QuoteId = quoteId == Guid.Empty ? null : quoteId,
            MessageId = Clip(email.MessageId?.Trim().Trim('<', '>'), 998),
            InReplyTo = Clip(email.InReplyTo, 998),
            FromAddress = Clip(email.FromAddress, 320),
            FromName = Clip(email.FromName, 500),
            Subject = Clip(email.Subject, 998),
            TextBody = ClipBody(email.TextBody),
            HtmlBody = ClipBody(email.HtmlBody),
            AttachmentMetadataJson = attachments.Length > 0 ? JsonSerializer.Serialize(attachments, JsonOptions) : null,
            SentAt = email.SentAt,
            ReceivedAt = receivedAt
        };
    }

    public IReadOnlyList<EmailAttachmentInfo> GetAttachments() =>
        string.IsNullOrWhiteSpace(AttachmentMetadataJson)
            ? []
            : JsonSerializer.Deserialize<EmailAttachmentInfo[]>(AttachmentMetadataJson, JsonOptions) ?? [];

    private static string? Clip(string? value, int maxLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return text is not null && text.Length > maxLength ? text[..maxLength] : text;
    }

    private static string? ClipBody(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length > RequestEmail.MaxBodyLength ? value[..RequestEmail.MaxBodyLength] : value;
}
