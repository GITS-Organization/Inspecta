namespace Inspecta.Domain.Entities;

/// <summary>
/// An email the client sent back in answer to a renewal reminder. It is kept with the reminder and never becomes a request or a "maybe" email.
/// The client's HTML is kept but never shown by the API: only the text is.
/// </summary>
public sealed class RenewalReminderReply
{
    private RenewalReminderReply() { }

    public Guid Id { get; private set; }
    public Guid ManagerId { get; private set; }
    public Guid ReminderId { get; private set; }

    /// <summary>Lowercase and without angle brackets.</summary>
    public string? MessageId { get; private set; }
    public string? InReplyTo { get; private set; }
    public string? FromAddress { get; private set; }
    public string? FromName { get; private set; }
    public string? Subject { get; private set; }
    public string? TextBody { get; private set; }
    public string? HtmlBody { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }

    public static RenewalReminderReply Create(Guid managerId, Guid reminderId, IncomingEmail email, DateTimeOffset receivedAt)
    {
        if (managerId == Guid.Empty || reminderId == Guid.Empty) throw new ArgumentException("Company and reminder are required.");
        ArgumentNullException.ThrowIfNull(email);

        return new RenewalReminderReply
        {
            Id = Guid.NewGuid(),
            ManagerId = managerId,
            ReminderId = reminderId,
            MessageId = Clip(email.MessageId?.Trim().Trim('<', '>').ToLowerInvariant(), 998),
            InReplyTo = Clip(email.InReplyTo, 998),
            FromAddress = Clip(email.FromAddress, 320),
            FromName = Clip(email.FromName, 500),
            Subject = Clip(email.Subject, 998),
            TextBody = ClipBody(email.TextBody),
            HtmlBody = ClipBody(email.HtmlBody),
            SentAt = email.SentAt,
            ReceivedAt = receivedAt
        };
    }

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
