namespace Inspecta.Domain.Entities;

public sealed class RequestItem
{
    private RequestItem() { }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public InspectionRequest? Request { get; private set; }
    public Guid? EquipmentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Type { get; private set; }
    public string? Category { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? Model { get; private set; }
    public string? Capacity { get; private set; }
    public string? PrNumber { get; private set; }
    public string? Location { get; private set; }
    public int Quantity { get; private set; }
    public string? SerialNumber { get; private set; }
    public string? IssueDescription { get; private set; }
    public List<InspectionCertificate> Certificates { get; private set; } = [];

    /// <summary>Throws when the values would not make a valid item, without creating anything.</summary>
    public static void Validate(string name, int quantity, Guid? equipmentId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Item name is required.", nameof(name));
        if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be at least one.");
        if (equipmentId == Guid.Empty) throw new ArgumentException("Equipment id cannot be empty.", nameof(equipmentId));
    }

    public static RequestItem Create(
        string name,
        int quantity,
        Guid? equipmentId = null,
        string? type = null,
        string? capacity = null,
        string? serialNumber = null,
        string? category = null,
        string? manufacturer = null,
        string? model = null,
        string? prNumber = null,
        string? location = null,
        string? issueDescription = null,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Item name is required.", nameof(name));
        if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be at least one.");
        if (equipmentId == Guid.Empty) throw new ArgumentException("Equipment id cannot be empty.", nameof(equipmentId));
        if (id == Guid.Empty) throw new ArgumentException("Item id cannot be empty.", nameof(id));

        return new RequestItem
        {
            Id = id ?? Guid.NewGuid(),
            EquipmentId = equipmentId,
            Name = name.Trim(),
            Type = type?.Trim(),
            Category = category?.Trim(),
            Manufacturer = manufacturer?.Trim(),
            Model = model?.Trim(),
            Capacity = capacity?.Trim(),
            PrNumber = prNumber?.Trim(),
            Location = location?.Trim(),
            Quantity = quantity,
            SerialNumber = serialNumber?.Trim(),
            IssueDescription = string.IsNullOrWhiteSpace(issueDescription) ? null : issueDescription.Trim()
        };
    }

    public void Update(
        string name,
        int quantity,
        Guid? equipmentId,
        string? type,
        string? capacity,
        string? serialNumber,
        string? location,
        string? issueDescription)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Item name is required.", nameof(name));
        if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be at least one.");
        if (equipmentId == Guid.Empty) throw new ArgumentException("Equipment id cannot be empty.", nameof(equipmentId));

        Name = name.Trim();
        Quantity = quantity;
        EquipmentId = equipmentId;
        Type = type?.Trim();
        Capacity = capacity?.Trim();
        SerialNumber = serialNumber?.Trim();
        Location = location?.Trim();
        IssueDescription = string.IsNullOrWhiteSpace(issueDescription) ? null : issueDescription.Trim();
    }

    public void MatchEquipment(Guid equipmentId)
    {
        if (equipmentId == Guid.Empty)
            throw new ArgumentException("Equipment is required.", nameof(equipmentId));
        EquipmentId = equipmentId;
    }

    public void UnmatchEquipment() => EquipmentId = null;
}
/// <summary>One equipment item as sent by a screen that saves the whole request: an existing item when <c>Id</c> is set, a new one otherwise.</summary>
public sealed record RequestItemSpec(
    Guid? Id,
    string Name,
    int Quantity,
    Guid? EquipmentId,
    string? Type,
    string? Capacity,
    string? SerialNumber,
    string? Location,
    string? IssueDescription);

/// <summary>Items a request revision added and removed, so the data layer can persist them in the same save.</summary>
public sealed record RequestItemChanges(IReadOnlyList<RequestItem> Added, IReadOnlyList<RequestItem> Removed);
