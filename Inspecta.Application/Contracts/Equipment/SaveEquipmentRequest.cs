using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Equipment;

/// <summary>Equipment registry record linked to a client.</summary>
public sealed record SaveEquipmentRequest
{
    public required Guid ClientId { get; init; }

    [Required, MaxLength(200)]
    public required string Name { get; init; }

    [MaxLength(200)]
    public string? Category { get; init; }

    [MaxLength(200)]
    public string? Manufacturer { get; init; }

    [MaxLength(200)]
    public string? Model { get; init; }

    [MaxLength(200)]
    public string? SerialNumber { get; init; }

    [MaxLength(100)]
    public string? Capacity { get; init; }
}

/// <summary>Inspection history update and next due date.</summary>
public sealed record RecordEquipmentInspectionRequest(DateTimeOffset InspectedAt, DateTimeOffset NextDueAt);