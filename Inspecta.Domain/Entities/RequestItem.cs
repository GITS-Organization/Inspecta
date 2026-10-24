namespace Inspecta.Domain.Entities;

public sealed class RequestItem
{
    private RequestItem() { }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid? EquipmentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Type { get; private set; }
    public string? Capacity { get; private set; }
    public int Quantity { get; private set; }
    public string? SerialNumber { get; private set; }

    public static RequestItem Create(string name, int quantity, Guid? equipmentId = null, string? type = null, string? capacity = null, string? serialNumber = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Item name is required.", nameof(name));
        if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be at least one.");

        return new RequestItem
        {
            Id = Guid.NewGuid(),
            EquipmentId = equipmentId,
            Name = name.Trim(),
            Type = type?.Trim(),
            Capacity = capacity?.Trim(),
            Quantity = quantity,
            SerialNumber = serialNumber?.Trim()
        };
    }
}