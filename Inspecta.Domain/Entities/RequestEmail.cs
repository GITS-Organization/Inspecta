using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Inspecta.Domain.Entities;

/// <summary>The original incoming email an <see cref="InspectionRequest"/> was created from.</summary>
public sealed class RequestEmail
{
    public const int MaxBodyLength = 1_000_000;
    public const int PreviewLength = 300;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex HtmlTag = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private RequestEmail() { }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public InspectionRequest? Request { get; private set; }
    public Guid CoordinatorId { get; private set; }
    public string MailboxUidValidity { get; private set; } = string.Empty;
    public long MailboxUid { get; private set; }
    public string? MessageId { get; private set; }
    public string? InReplyTo { get; private set; }
    public string? References { get; private set; }
    public string? FromAddress { get; private set; }
    public string? FromName { get; private set; }
    public string? Subject { get; private set; }
    public string? ToAddressesJson { get; private set; }
    public string? CcAddressesJson { get; private set; }
    public string? BccAddressesJson { get; private set; }
    public string? ReplyToAddressesJson { get; private set; }
    public string? AttachmentMetadataJson { get; private set; }
    public string? TextBody { get; private set; }
    public string? HtmlBody { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }

    public static RequestEmail Create(
        Guid requestId,
        Guid coordinatorId,
        string mailboxUidValidity,
        long mailboxUid,
        string? textBody,
        string? htmlBody,
        string? fromAddress,
        string? fromName,
        string? subject,
        IEnumerable<EmailAddressInfo>? toAddresses,
        IEnumerable<EmailAddressInfo>? ccAddresses,
        IEnumerable<EmailAddressInfo>? bccAddresses,
        IEnumerable<EmailAddressInfo>? replyToAddresses,
        IEnumerable<EmailAttachmentInfo>? attachments,
        string? messageId,
        string? inReplyTo,
        string? references,
        DateTimeOffset? sentAt,
        DateTimeOffset receivedAt)
    {
        if (requestId == Guid.Empty || coordinatorId == Guid.Empty)
            throw new ArgumentException("Request and coordinator ids are required.");
        if (string.IsNullOrWhiteSpace(mailboxUidValidity))
            throw new ArgumentException("Mailbox UID validity is required.", nameof(mailboxUidValidity));
        if (mailboxUid <= 0)
            throw new ArgumentOutOfRangeException(nameof(mailboxUid));

        return new RequestEmail
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            CoordinatorId = coordinatorId,
            MailboxUidValidity = mailboxUidValidity.Trim(),
            MailboxUid = mailboxUid,
            MessageId = Clip(messageId, 998),
            InReplyTo = Clip(inReplyTo, 998),
            References = Normalize(references),
            FromAddress = Clip(fromAddress, 320),
            FromName = Clip(fromName, 500),
            Subject = Clip(subject, 998),
            ToAddressesJson = Serialize(toAddresses),
            CcAddressesJson = Serialize(ccAddresses),
            BccAddressesJson = Serialize(bccAddresses),
            ReplyToAddressesJson = Serialize(replyToAddresses),
            AttachmentMetadataJson = Serialize(attachments),
            TextBody = ClipBody(textBody),
            HtmlBody = ClipBody(htmlBody),
            SentAt = sentAt,
            ReceivedAt = receivedAt
        };
    }

    /// <summary>Short single-line text preview of the message body for list screens.</summary>
    public static string? BuildPreview(string? textBody, string? htmlBody)
    {
        var source = !string.IsNullOrWhiteSpace(textBody)
            ? textBody
            : string.IsNullOrWhiteSpace(htmlBody)
                ? null
                : WebUtility.HtmlDecode(HtmlTag.Replace(htmlBody, " "));
        if (source is null) return null;

        var collapsed = Whitespace.Replace(source, " ").Trim();
        if (collapsed.Length == 0) return null;
        return collapsed.Length <= PreviewLength ? collapsed : collapsed[..PreviewLength].TrimEnd() + "…";
    }

    public IReadOnlyList<EmailAddressInfo> GetToAddresses() => Deserialize<EmailAddressInfo>(ToAddressesJson);
    public IReadOnlyList<EmailAddressInfo> GetCcAddresses() => Deserialize<EmailAddressInfo>(CcAddressesJson);
    public IReadOnlyList<EmailAddressInfo> GetBccAddresses() => Deserialize<EmailAddressInfo>(BccAddressesJson);
    public IReadOnlyList<EmailAddressInfo> GetReplyToAddresses() => Deserialize<EmailAddressInfo>(ReplyToAddressesJson);
    public IReadOnlyList<EmailAttachmentInfo> GetAttachments() => Deserialize<EmailAttachmentInfo>(AttachmentMetadataJson);

    private static string? Serialize<T>(IEnumerable<T>? values)
    {
        var items = values?.ToArray();
        return items is { Length: > 0 } ? JsonSerializer.Serialize(items, JsonOptions) : null;
    }

    private static IReadOnlyList<T> Deserialize<T>(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : JsonSerializer.Deserialize<T[]>(value, JsonOptions) ?? [];

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Clip(string? value, int maxLength)
    {
        var normalized = Normalize(value);
        return normalized is { Length: > 0 } && normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }

    private static string? ClipBody(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length > MaxBodyLength ? value[..MaxBodyLength] : value;
}

/// <summary>Parsed fields of one message fetched from a coordinator mailbox.</summary>
public sealed record IncomingEmail(
    string MailboxUidValidity,
    long MailboxUid,
    string? TextBody,
    string? HtmlBody,
    string? FromAddress,
    string? FromName,
    string? Subject,
    IReadOnlyList<EmailAddressInfo> To,
    IReadOnlyList<EmailAddressInfo> Cc,
    IReadOnlyList<EmailAddressInfo> Bcc,
    IReadOnlyList<EmailAddressInfo> ReplyTo,
    IReadOnlyList<EmailAttachmentInfo> Attachments,
    string? MessageId,
    string? InReplyTo,
    string? References,
    DateTimeOffset? SentAt);
