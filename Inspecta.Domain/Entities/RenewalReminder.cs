using System.Text.Json;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

/// <summary>
/// An email a coordinator sent to a client about machines that are due for inspection. It goes out only when the coordinator presses Send, from their own
/// mailbox. The row is written before the mail server is contacted, so every attempt is recorded, a double click sends once (the key is unique per company)
/// and a retry reuses the same message.
/// </summary>
public sealed class RenewalReminder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private RenewalReminder() { }

    public Guid Id { get; private set; }
    public Guid ManagerId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid SentById { get; private set; }

    /// <summary>Made by the screen when the reminder is opened and reused for a retry: one key, one email.</summary>
    public Guid SendKey { get; private set; }
    public OutboundEmailStatus Status { get; private set; }
    public string ToAddress { get; private set; } = string.Empty;
    public string? ToName { get; private set; }
    public string FromAddress { get; private set; } = string.Empty;
    public string? FromName { get; private set; }
    public string Subject { get; private set; } = string.Empty;

    /// <summary>The coordinator's own text, without the table of machines and the signature.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>What the email showed: the machines with their dates, as they were when it was sent.</summary>
    public string MachinesJson { get; private set; } = "[]";

    /// <summary>Lowercase and without angle brackets, as the replies name it.</summary>
    public string MessageId { get; private set; } = string.Empty;
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? LastReplyAt { get; private set; }
    public Guid? RequestId { get; private set; }
    public DateTimeOffset? RequestCreatedAt { get; private set; }
    public Guid? RequestCreatedById { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedById { get; private set; }

    public bool IsStalePending => Status == OutboundEmailStatus.Pending && UpdatedAt < DateTimeOffset.UtcNow - OutboundEmail.PendingTimeout;

    /// <summary>Now, to the microsecond: that is all the database keeps, and the date of the last change is the token two clicks compete with, so it must compare equal after a round trip.</summary>
    private static DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Ticks - now.Ticks % 10, TimeSpan.Zero);
    }

    /// <summary>sending, failed, sent, replied, requested or closed.</summary>
    public string State =>
        RequestId is not null ? "requested"
        : ClosedAt is not null ? "closed"
        : LastReplyAt is not null ? "replied"
        : Status == OutboundEmailStatus.Failed || IsStalePending ? "failed"
        : Status == OutboundEmailStatus.Pending ? "sending"
        : "sent";

    public static RenewalReminder Create(Guid managerId, Guid clientId, Guid sentById, Guid sendKey, string messageIdDomain)
    {
        if (managerId == Guid.Empty || clientId == Guid.Empty || sentById == Guid.Empty || sendKey == Guid.Empty)
            throw new ArgumentException("Company, client, sender and key are required.");
        if (string.IsNullOrWhiteSpace(messageIdDomain)) throw new ArgumentException("A domain for the Message-ID is required.", nameof(messageIdDomain));

        var now = Now();
        return new RenewalReminder
        {
            Id = Guid.NewGuid(),
            ManagerId = managerId,
            ClientId = clientId,
            SentById = sentById,
            SendKey = sendKey,
            Status = OutboundEmailStatus.Pending,
            MessageId = $"{Guid.NewGuid():N}@{messageIdDomain.Trim()}".ToLowerInvariant(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Fills the message for a new attempt (the first or a retry) and counts it.</summary>
    public void BeginAttempt(
        string toAddress, string? toName, string fromAddress, string? fromName, string subject, string message, IEnumerable<ReminderMachine> machines)
    {
        if (Status == OutboundEmailStatus.Sent) throw new InvalidOperationException("This reminder was already sent.");
        if (string.IsNullOrWhiteSpace(toAddress)) throw new ArgumentException("Recipient is required.", nameof(toAddress));
        if (string.IsNullOrWhiteSpace(fromAddress)) throw new ArgumentException("Sender is required.", nameof(fromAddress));
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Subject is required.", nameof(subject));

        ToAddress = toAddress.Trim();
        ToName = string.IsNullOrWhiteSpace(toName) ? null : toName.Trim();
        FromAddress = fromAddress.Trim();
        FromName = string.IsNullOrWhiteSpace(fromName) ? null : fromName.Trim();
        Subject = subject.Trim();
        Message = message ?? string.Empty;
        MachinesJson = JsonSerializer.Serialize(machines.ToList(), JsonOptions);
        Status = OutboundEmailStatus.Pending;
        Attempts++;
        LastError = null;
        UpdatedAt = Now();
    }

    public void MarkSent()
    {
        Status = OutboundEmailStatus.Sent;
        SentAt = Now();
        UpdatedAt = SentAt.Value;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        // The client already answered: the email did arrive, whatever the mail server's own answer said.
        if (LastReplyAt is not null) return;
        if (Status == OutboundEmailStatus.Sent) throw new InvalidOperationException("A sent reminder cannot fail.");
        Status = OutboundEmailStatus.Failed;
        LastError = string.IsNullOrWhiteSpace(error) ? "Unknown error." : error.Length > 500 ? error[..500] : error;
        UpdatedAt = Now();
    }

    public IReadOnlyList<ReminderMachine> GetMachines() =>
        JsonSerializer.Deserialize<List<ReminderMachine>>(MachinesJson, JsonOptions) ?? [];

    /// <summary>
    /// The client answered (any number of times): only the newest date is kept. An answer proves the email arrived, even when the mail server's own
    /// answer was lost (the row says failed or still sending): the reminder then counts as sent.
    /// </summary>
    public void RecordReply(DateTimeOffset at)
    {
        if (LastReplyAt is null || at > LastReplyAt) LastReplyAt = at;
        if (Status != OutboundEmailStatus.Sent)
        {
            Status = OutboundEmailStatus.Sent;
            SentAt ??= at;
            LastError = null;
            // The status changed: a retry that read the row before this must lose.
            UpdatedAt = at > UpdatedAt ? at : UpdatedAt.AddTicks(10);
        }
    }

    /// <summary>The reminder became a request. Once.</summary>
    public void LinkRequest(Guid requestId, Guid actorId, DateTimeOffset at)
    {
        if (RequestId is not null) throw new InvalidOperationException("A request was already created from this reminder.");
        if (ClosedAt is not null) throw new InvalidOperationException("This reminder was closed.");
        RequestId = requestId;
        RequestCreatedAt = at;
        RequestCreatedById = actorId;
        UpdatedAt = at;
    }

    /// <summary>The client said no, or it was handled another way: the reminder ends without a request.</summary>
    public void Close(Guid actorId, DateTimeOffset at)
    {
        if (RequestId is not null) throw new InvalidOperationException("A request was already created from this reminder.");
        if (ClosedAt is not null) return;
        ClosedAt = at;
        ClosedById = actorId;
        UpdatedAt = at;
    }
}

/// <summary>A machine as the reminder showed it: stored with the reminder so it reads the same later.</summary>
public sealed record ReminderMachine(Guid EquipmentId, string Name, string? SerialNumber, DateOnly? LastInspectedOn, DateOnly NextDueOn);
