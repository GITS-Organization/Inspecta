using System.ComponentModel.DataAnnotations;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Equipment;

/// <summary>Payload for creating or updating an inspection item linked to a client.</summary>
public sealed record SaveEquipmentRequest
{
    public required Guid ClientId { get; init; }

    [Required, MaxLength(200)]
    public required string Name { get; init; }

    public ItemType? ItemType { get; init; }

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

    [MaxLength(500)]
    public string? LocationInsideClientLocation { get; init; }

    public string? Notes { get; init; }

    public IReadOnlyList<string>? Labels { get; init; }

    public IReadOnlyList<ItemDate>? Dates { get; init; }

    public IReadOnlyList<ItemChecklistEntry>? Checklist { get; init; }

    public IReadOnlyList<ItemAttachment>? Attachments { get; init; }
}

/// <summary>Inspection history update and next due date.</summary>
public sealed record RecordEquipmentInspectionRequest(DateTimeOffset InspectedAt, DateTimeOffset NextDueAt);