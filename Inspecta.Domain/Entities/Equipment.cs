namespace Inspecta.Domain.Entities;

public sealed class Equipment
{
    private Equipment() { }

    public Guid Id { get; private set; }
    public string? LegacyId { get; private set; }
    public Guid ClientId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Category { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? Model { get; private set; }
    public string? SerialNumber { get; private set; }
    public string? Capacity { get; private set; }
    public DateTimeOffset? LastInspectionAt { get; private set; }
    public DateTimeOffset? NextInspectionDueAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public static Equipment Create(Guid clientId, string name, string? legacyId = null)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("Client is required.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Equipment name is required.", nameof(name));
        return new Equipment { Id = Guid.NewGuid(), ClientId = clientId, Name = name.Trim(), LegacyId = legacyId };
    }

    public void UpdateDetails(string name, string? category, string? manufacturer, string? model, string? serialNumber, string? capacity)
    {
        if (ArchivedAt is not null) throw new InvalidOperationException("Archived equipment cannot be edited.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Equipment name is required.", nameof(name));
        Name = name.Trim();
        Category = category?.Trim();
        Manufacturer = manufacturer?.Trim();
        Model = model?.Trim();
        SerialNumber = serialNumber?.Trim();
        Capacity = capacity?.Trim();
    }

    public void RecordInspection(DateTimeOffset inspectedAt, DateTimeOffset nextDueAt)
    {
        if (nextDueAt <= inspectedAt) throw new ArgumentException("Next inspection date must be after the inspection date.", nameof(nextDueAt));
        LastInspectionAt = inspectedAt;
        NextInspectionDueAt = nextDueAt;
    }

    public void Archive() => ArchivedAt ??= DateTimeOffset.UtcNow;
}