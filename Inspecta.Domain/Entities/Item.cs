using System.Text.Json;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

public sealed class Item
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private Item() { }

    public Guid Id { get; private set; }
    public string? LegacyId { get; private set; }
    public Guid ClientId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Type => Name;
    public ItemType ItemType { get; private set; } = ItemType.Equipment;
    public string? Category { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? Model { get; private set; }
    public short? YearOfManufacture { get; private set; }
    public string? SerialNumber { get; private set; }
    public string? PrNumber { get; private set; }
    public string? Capacity { get; private set; }
    public string? Location { get; private set; }
    public string? LocationInsideClientLocation => Location;
    public string? AssetTag { get; private set; }
    public string? OwnerReference { get; private set; }
    public string? Notes { get; private set; }
    public string LabelsJson { get; private set; } = "[]";
    public string DatesJson { get; private set; } = "[]";
    public string ChecklistJson { get; private set; } = "[]";
    public string AttachmentsJson { get; private set; } = "[]";
    public bool HasAngle { get; private set; }
    public EquipmentStatus Status { get; private set; } = EquipmentStatus.Active;
    public DateOnly? CertificateExpiryDate { get; private set; }
    public DateOnly? LastInspectedOn { get; private set; }
    public CertificateResult? LastInspectionResult { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? LastInspectionAt { get; private set; }
    public DateTimeOffset? NextInspectionDueAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public IReadOnlyList<string> GetLabels() => Deserialize<List<string>>(LabelsJson);
    public IReadOnlyList<ItemDate> GetDates() => Deserialize<List<ItemDate>>(DatesJson);
    public IReadOnlyList<ItemChecklistEntry> GetChecklist() => Deserialize<List<ItemChecklistEntry>>(ChecklistJson);
    public IReadOnlyList<ItemAttachment> GetAttachments() => Deserialize<List<ItemAttachment>>(AttachmentsJson);

    public static Item Create(Guid clientId, string name, string? legacyId = null)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("Client is required.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Item name is required.", nameof(name));
        return new Item { Id = Guid.NewGuid(), ClientId = clientId, Name = name.Trim(), LegacyId = legacyId };
    }

    public static Item Register(
        Guid clientId,
        Guid categoryId,
        string type,
        DateOnly certificateExpiryDate,
        bool hasAngle,
        bool allowExpired = false,
        string? manufacturer = null,
        string? model = null,
        short? yearOfManufacture = null,
        string? serialNumber = null,
        string? prNumber = null,
        string? capacity = null,
        string? location = null,
        string? ownerReference = null,
        string? assetTag = null,
        string? notes = null,
        string? legacyId = null,
        DateOnly? today = null,
        ItemType itemType = ItemType.Equipment)
    {
        if (clientId == Guid.Empty)
            throw new ArgumentException("Client is required.", nameof(clientId));
        if (categoryId == Guid.Empty)
            throw new ArgumentException("Category is required.", nameof(categoryId));
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Item type is required.", nameof(type));
        if (!Enum.IsDefined(itemType))
            throw new ArgumentOutOfRangeException(nameof(itemType));
        if (!allowExpired && certificateExpiryDate < (today ?? DateOnly.FromDateTime(DateTime.UtcNow)))
            throw new ArgumentException("Item certificate expiry cannot be in the past.", nameof(certificateExpiryDate));
        if (yearOfManufacture is < 1800 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(yearOfManufacture));

        return new Item
        {
            Id = Guid.NewGuid(),
            LegacyId = legacyId,
            ClientId = clientId,
            Name = type.Trim(),
            ItemType = itemType,
            CategoryId = categoryId,
            Manufacturer = Normalize(manufacturer),
            Model = Normalize(model),
            YearOfManufacture = yearOfManufacture,
            SerialNumber = Normalize(serialNumber),
            PrNumber = Normalize(prNumber),
            Capacity = Normalize(capacity),
            Location = Normalize(location),
            OwnerReference = Normalize(ownerReference),
            AssetTag = Normalize(assetTag),
            Notes = Normalize(notes),
            HasAngle = hasAngle,
            Status = EquipmentStatus.Active,
            CertificateExpiryDate = certificateExpiryDate
        };
    }

    public void UpdateDetails(
        string name,
        string? category,
        string? manufacturer,
        string? model,
        string? serialNumber,
        string? capacity,
        ItemType? itemType = null,
        string? locationInsideClientLocation = null,
        string? notes = null)
    {
        EnsureEditable();
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Item name is required.", nameof(name));
        if (itemType is not null && !Enum.IsDefined(itemType.Value))
            throw new ArgumentOutOfRangeException(nameof(itemType));
        Name = name.Trim();
        Category = Normalize(category);
        Manufacturer = Normalize(manufacturer);
        Model = Normalize(model);
        SerialNumber = Normalize(serialNumber);
        Capacity = Normalize(capacity);
        if (itemType is not null)
            ItemType = itemType.Value;
        if (locationInsideClientLocation is not null)
            Location = Normalize(locationInsideClientLocation);
        if (notes is not null)
            Notes = Normalize(notes);
    }

    public void UpdateMetadata(
        IEnumerable<string>? labels = null,
        IEnumerable<ItemDate>? dates = null,
        IEnumerable<ItemChecklistEntry>? checklist = null,
        IEnumerable<ItemAttachment>? attachments = null)
    {
        EnsureEditable();
        if (labels is not null)
        {
            var normalizedLabels = labels
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Select(label => label.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            LabelsJson = JsonSerializer.Serialize(normalizedLabels, JsonOptions);
        }

        if (dates is not null)
        {
            var dateValues = dates.Select(date => ItemDate.Create(date.Name, date.Value)).ToArray();
            DatesJson = JsonSerializer.Serialize(dateValues, JsonOptions);
        }

        if (checklist is not null)
        {
            var checklistValues = checklist
                .Select(entry => ItemChecklistEntry.Create(entry.Name, entry.IsCompleted))
                .ToArray();
            ChecklistJson = JsonSerializer.Serialize(checklistValues, JsonOptions);
        }

        if (attachments is not null)
        {
            var attachmentValues = attachments
                .Select(attachment => ItemAttachment.Create(
                    attachment.FileName, attachment.StorageKey, attachment.ContentType, attachment.SizeBytes))
                .ToArray();
            AttachmentsJson = JsonSerializer.Serialize(attachmentValues, JsonOptions);
        }
    }

    public void UpdateRegisterDetails(
        string type,
        Guid categoryId,
        string? manufacturer,
        string? model,
        short? yearOfManufacture,
        string? serialNumber,
        string? prNumber,
        string? capacity,
        string? location,
        string? ownerReference,
        string? assetTag = null,
        string? notes = null)
    {
        EnsureEditable();
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Item type is required.", nameof(type));
        if (categoryId == Guid.Empty)
            throw new ArgumentException("Category is required.", nameof(categoryId));
        if (yearOfManufacture is < 1800 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(yearOfManufacture));

        Name = type.Trim();
        CategoryId = categoryId;
        Manufacturer = Normalize(manufacturer);
        Model = Normalize(model);
        YearOfManufacture = yearOfManufacture;
        SerialNumber = Normalize(serialNumber);
        PrNumber = Normalize(prNumber);
        Capacity = Normalize(capacity);
        Location = Normalize(location);
        OwnerReference = Normalize(ownerReference);
        AssetTag = Normalize(assetTag);
        Notes = Normalize(notes);
    }

    public void UpdateInspectionRequirements(bool hasAngle, DateOnly certificateExpiryDate, bool allowExpired = false, DateOnly? today = null)
    {
        EnsureEditable();
        if (!allowExpired && certificateExpiryDate < (today ?? DateOnly.FromDateTime(DateTime.UtcNow)))
            throw new ArgumentException("Item certificate expiry cannot be in the past.", nameof(certificateExpiryDate));

        HasAngle = hasAngle;
        CertificateExpiryDate = certificateExpiryDate;
    }

    public void RecordInspection(DateTimeOffset inspectedAt, DateTimeOffset nextDueAt)
    {
        EnsureEditable();
        if (nextDueAt <= inspectedAt) throw new ArgumentException("Next inspection date must be after the inspection date.", nameof(nextDueAt));
        LastInspectionAt = inspectedAt;
        NextInspectionDueAt = nextDueAt;
        LastInspectedOn = DateOnly.FromDateTime(inspectedAt.UtcDateTime);
        CertificateExpiryDate = DateOnly.FromDateTime(nextDueAt.UtcDateTime);
    }

    public void RecordInspection(DateOnly inspectedOn, DateOnly nextDueOn, CertificateResult result)
    {
        EnsureEditable();
        if (nextDueOn <= inspectedOn)
            throw new ArgumentException("Next inspection date must be after the inspection date.", nameof(nextDueOn));

        LastInspectedOn = inspectedOn;
        LastInspectionResult = result;
        CertificateExpiryDate = nextDueOn;
        LastInspectionAt = new DateTimeOffset(inspectedOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        NextInspectionDueAt = new DateTimeOffset(nextDueOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    public void Retire()
    {
        EnsureEditable();
        Status = EquipmentStatus.Retired;
    }

    public bool IsDueSoon(DateOnly today) =>
        Status == EquipmentStatus.Active &&
        CertificateExpiryDate is { } expiry &&
        expiry >= today &&
        expiry <= today.AddDays(30);

    public void Archive()
    {
        ArchivedAt ??= DateTimeOffset.UtcNow;
        IsArchived = true;
    }

    private void EnsureEditable()
    {
        if (ArchivedAt is not null || IsArchived)
            throw new InvalidOperationException("Archived items cannot be edited.");
    }

    private static T Deserialize<T>(string json) where T : new() =>
        JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T();

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
