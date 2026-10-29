using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

/// <summary>
/// An email Inspecta sends for a quotation (the offer or the acceptance with its PDF). The row is written
/// before the mail server is contacted, so every delivery is recorded and a retry reuses the same message.
/// </summary>
public sealed class OutboundEmail
{
    /// <summary>A Pending attempt older than this is treated as abandoned (for example after a crash) and can be retried.</summary>
    public static readonly TimeSpan PendingTimeout = TimeSpan.FromMinutes(5);

    private OutboundEmail() { }

    public Guid Id { get; private set; }
    public Guid ManagerId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid QuoteId { get; private set; }
    public OutboundEmailKind Kind { get; private set; }
    public OutboundEmailStatus Status { get; private set; }
    public string ToAddress { get; private set; } = string.Empty;
    public string? ToName { get; private set; }
    public string FromAddress { get; private set; } = string.Empty;
    public string? FromName { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string BodyText { get; private set; } = string.Empty;
    public string MessageId { get; private set; } = string.Empty;
    public string? InReplyTo { get; private set; }
    public string? References { get; private set; }
    public string AttachmentFileName { get; private set; } = string.Empty;
    public byte[] AttachmentContent { get; private set; } = [];
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }

    public bool IsStalePending => Status == OutboundEmailStatus.Pending && UpdatedAt < DateTimeOffset.UtcNow - PendingTimeout;

    public static OutboundEmail Create(
        Guid managerId,
        Guid requestId,
        Guid quoteId,
        OutboundEmailKind kind,
        string messageIdDomain)
    {
        if (managerId == Guid.Empty || requestId == Guid.Empty || quoteId == Guid.Empty)
            throw new ArgumentException("Manager, request and quote are required.");
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.IsNullOrWhiteSpace(messageIdDomain))
            throw new ArgumentException("A domain for the Message-ID is required.", nameof(messageIdDomain));

        var now = DateTimeOffset.UtcNow;
        return new OutboundEmail
        {
            Id = Guid.NewGuid(),
            ManagerId = managerId,
            RequestId = requestId,
            QuoteId = quoteId,
            Kind = kind,
            Status = OutboundEmailStatus.Pending,
            MessageId = $"{Guid.NewGuid():N}@{messageIdDomain.Trim()}",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Fills the message for a new attempt (the first one or a retry) and counts it.</summary>
    public void BeginAttempt(
        string toAddress,
        string? toName,
        string fromAddress,
        string? fromName,
        string subject,
        string bodyText,
        string? inReplyTo,
        string? references,
        string attachmentFileName,
        byte[] attachmentContent)
    {
        if (Status == OutboundEmailStatus.Sent)
            throw new InvalidOperationException("This email was already sent.");
        if (string.IsNullOrWhiteSpace(toAddress)) throw new ArgumentException("Recipient is required.", nameof(toAddress));
        if (string.IsNullOrWhiteSpace(fromAddress)) throw new ArgumentException("Sender is required.", nameof(fromAddress));
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Subject is required.", nameof(subject));
        ArgumentNullException.ThrowIfNull(attachmentContent);

        ToAddress = toAddress.Trim();
        ToName = string.IsNullOrWhiteSpace(toName) ? null : toName.Trim();
        FromAddress = fromAddress.Trim();
        FromName = string.IsNullOrWhiteSpace(fromName) ? null : fromName.Trim();
        Subject = subject.Trim();
        BodyText = bodyText ?? string.Empty;
        InReplyTo = string.IsNullOrWhiteSpace(inReplyTo) ? null : inReplyTo.Trim();
        References = string.IsNullOrWhiteSpace(references) ? null : references.Trim();
        AttachmentFileName = attachmentFileName;
        AttachmentContent = attachmentContent;
        Status = OutboundEmailStatus.Pending;
        Attempts++;
        LastError = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkSent()
    {
        Status = OutboundEmailStatus.Sent;
        SentAt = DateTimeOffset.UtcNow;
        UpdatedAt = SentAt.Value;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        if (Status == OutboundEmailStatus.Sent)
            throw new InvalidOperationException("A sent email cannot fail.");

        Status = OutboundEmailStatus.Failed;
        LastError = string.IsNullOrWhiteSpace(error) ? "Unknown error." : error.Length > 500 ? error[..500] : error;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
