using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

public sealed class QuotationDraft
{
    private QuotationDraft() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid CoordinatorId { get; private set; }
    public string? ClientName { get; private set; }
    public string? Location { get; private set; }
    public string? MachineDetails { get; private set; }
    public string RawEmailBody { get; private set; } = string.Empty;
    public string? SenderEmail { get; private set; }
    public string? EmailSubject { get; private set; }
    public QuotationDraftStatus Status { get; private set; } = QuotationDraftStatus.Draft;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string SourceUidValidity { get; private set; } = string.Empty;
    public uint SourceUid { get; private set; }

    public static QuotationDraft Create(
        Guid tenantId,
        Guid coordinatorId,
        string? clientName,
        string? location,
        string? machineDetails,
        string rawEmailBody,
        string? senderEmail,
        string? emailSubject,
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
            SenderEmail = Normalize(senderEmail),
            EmailSubject = Normalize(emailSubject),
            SourceUidValidity = sourceUidValidity,
            SourceUid = sourceUid,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }

    public void UpdateDetails(string? clientName, string? location, string? machineDetails)
    {
        ClientName = Normalize(clientName);
        Location = Normalize(location);
        MachineDetails = Normalize(machineDetails);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
