using Inspecta.Domain.Enums;
using System.Text.Json;

namespace Inspecta.Domain.Entities;

public sealed class QuotationDraft
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private QuotationDraft() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid CoordinatorId { get; private set; }
    public string? ClientName { get; private set; }
    public string? Location { get; private set; }
    public string? MachineDetails { get; private set; }
    public long? AmountPiastres { get; private set; }
    public DateTimeOffset? ValidUntil { get; private set; }
    public string RawEmailBody { get; private set; } = string.Empty;
    public string? EmailHtmlBody { get; private set; }
    public string? SenderEmail { get; private set; }
    public string? SenderName { get; private set; }
    public string? EmailSubject { get; private set; }
    public string? ToAddressesJson { get; private set; }
    public string? CcAddressesJson { get; private set; }
    public string? BccAddressesJson { get; private set; }
    public string? ReplyToAddressesJson { get; private set; }
    public string? AttachmentMetadataJson { get; private set; }
    public string? EmailMessageId { get; private set; }
    public string? InReplyTo { get; private set; }
    public string? References { get; private set; }
    public DateTimeOffset? EmailDate { get; private set; }
    public string CoordinatorName => Coordinator.FullName;
    public QuotationDraftStatus Status { get; private set; } = QuotationDraftStatus.Draft;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string SourceUidValidity { get; private set; } = string.Empty;
    public uint SourceUid { get; private set; }
    public AppUser Coordinator { get; private set; } = null!;

    public static QuotationDraft Create(
        Guid tenantId,
        Guid coordinatorId,
        string? clientName,
        string? location,
        string? machineDetails,
        string rawEmailBody,
        string? emailHtmlBody,
        string? senderEmail,
        string? senderName,
        string? emailSubject,
        IEnumerable<EmailAddressInfo>? toAddresses,
        IEnumerable<EmailAddressInfo>? ccAddresses,
        IEnumerable<EmailAddressInfo>? bccAddresses,
        IEnumerable<EmailAddressInfo>? replyToAddresses,
        IEnumerable<EmailAttachmentInfo>? attachments,
        string? emailMessageId,
        string? inReplyTo,
        string? references,
        DateTimeOffset? emailDate,
        string sourceUidValidity,
        uint sourceUid,
        DateTimeOffset? createdAt = null)
    {
        if (tenantId == Guid.Empty || coordinatorId == Guid.Empty)
            throw new ArgumentException("Tenant and coordinator ids are required.");
        if (string.IsNullOrWhiteSpace(rawEmailBody))
            throw new ArgumentException("Raw email body is required.", nameof(rawEmailBody));
        if (string.IsNullOrWhiteSpace(sourceUidValidity))
            throw new ArgumentException("Source UID validity is required.", nameof(sourceUidValidity));
        if (sourceUid == 0)
            throw new ArgumentOutOfRangeException(nameof(sourceUid));

        return new QuotationDraft
        {
            TenantId = tenantId,
            CoordinatorId = coordinatorId,
            ClientName = Normalize(clientName),
            Location = Normalize(location),
            MachineDetails = Normalize(machineDetails),
            RawEmailBody = rawEmailBody,
            EmailHtmlBody = emailHtmlBody,
            SenderEmail = Normalize(senderEmail),
            SenderName = Normalize(senderName),
            EmailSubject = Normalize(emailSubject),
            ToAddressesJson = Serialize(toAddresses),
            CcAddressesJson = Serialize(ccAddresses),
            BccAddressesJson = Serialize(bccAddresses),
            ReplyToAddressesJson = Serialize(replyToAddresses),
            AttachmentMetadataJson = Serialize(attachments),
            EmailMessageId = Normalize(emailMessageId),
            InReplyTo = Normalize(inReplyTo),
            References = Normalize(references),
            EmailDate = emailDate,
            SourceUidValidity = sourceUidValidity,
            SourceUid = sourceUid,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }

    public void UpdateDetails(
        string? clientName,
        string? location,
        string? machineDetails,
        long? amountPiastres,
        DateTimeOffset? validUntil)
    {
        if (Status is not (QuotationDraftStatus.Draft or QuotationDraftStatus.CounterOffered))
            throw new InvalidOperationException("Only a draft or counter-offered quotation can be edited.");
        if (amountPiastres is <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountPiastres), "Quotation amount must be positive.");
        if (validUntil is not null && validUntil <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Quotation validity date must be in the future.", nameof(validUntil));

        ClientName = Normalize(clientName);
        Location = Normalize(location);
        MachineDetails = Normalize(machineDetails);
        AmountPiastres = amountPiastres;
        ValidUntil = validUntil;
    }

    public void SetDecision(QuotationDraftStatus status)
    {
        if (Status is not (QuotationDraftStatus.Draft or QuotationDraftStatus.CounterOffered))
            throw new InvalidOperationException("Only a draft or counter-offered quotation can receive a new decision.");

        if (status is not (QuotationDraftStatus.CounterOffered or QuotationDraftStatus.Accepted or QuotationDraftStatus.Rejected))
            throw new ArgumentOutOfRangeException(nameof(status), "Only CounterOffered, Accepted, or Rejected are coordinator decisions.");
        if (status == QuotationDraftStatus.CounterOffered && AmountPiastres is null)
            throw new InvalidOperationException("Set a quotation amount before making a counter-offer.");

        Status = status;
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
}

public sealed record EmailAddressInfo(string Address, string? Name);

public sealed record EmailAttachmentInfo(string FileName, string ContentType, long Size);
