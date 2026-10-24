namespace Inspecta.Application.Contracts.Equipment;

/// <summary>Equipment registry item and its latest inspection dates.</summary>
public sealed record EquipmentResponse(
    Guid Id,
    Guid ClientId,
    string Name,
    string? Category,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? Capacity,
    DateTimeOffset? LastInspectionAt,
    DateTimeOffset? NextInspectionDueAt,
    DateTimeOffset? ArchivedAt);