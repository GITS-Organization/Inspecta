namespace Inspecta.Domain.Entities;

/// <summary>An email that carried the approved certificates of a job to the client.</summary>
public sealed class CertificateDelivery
{
    private CertificateDelivery() { }

    public Guid Id { get; private set; }
    public Guid ManagerId { get; private set; }
    public Guid RequestId { get; private set; }
    public string ToAddress { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string BodyText { get; private set; } = string.Empty;
    public string MessageId { get; private set; } = string.Empty;
    public string AttachmentNames { get; private set; } = string.Empty;
    public bool Sent { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }

    public static CertificateDelivery Create(
        Guid managerId, Guid requestId, string toAddress, string subject, string bodyText, string messageId, IEnumerable<string> attachmentNames)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("Request is required.", nameof(requestId));
        if (string.IsNullOrWhiteSpace(toAddress)) throw new ArgumentException("Recipient is required.", nameof(toAddress));
        return new CertificateDelivery
        {
            Id = Guid.NewGuid(),
            ManagerId = managerId,
            RequestId = requestId,
            ToAddress = toAddress.Trim(),
            Subject = subject,
            BodyText = bodyText,
            MessageId = messageId,
            AttachmentNames = string.Join("; ", attachmentNames),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkAttempt() => Attempts++;

    public void MarkSent()
    {
        Sent = true;
        SentAt = DateTimeOffset.UtcNow;
        LastError = null;
    }

    public void MarkFailed(string error) => LastError = error.Length > 1000 ? error[..1000] : error;
}
