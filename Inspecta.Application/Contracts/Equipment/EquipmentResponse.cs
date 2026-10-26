using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Equipment;

/// <summary>Registered inspection item, metadata, and inspection dates.</summary>
public sealed record EquipmentResponse(
    Guid Id,
    Guid ClientId,
    string Name,
    string? Category,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? Capacity,
    ItemType ItemType,
    string? LocationInsideClientLocation,
    string? Notes,
    IReadOnlyList<string> Labels,
    IReadOnlyList<ItemDate> Dates,
    IReadOnlyList<ItemChecklistEntry> Checklist,
    IReadOnlyList<ItemAttachment> Attachments,
    DateOnly? CertificateExpiryDate,
    DateOnly? LastInspectedOn,
    DateTimeOffset? LastInspectionAt,
    DateTimeOffset? NextInspectionDueAt,
    DateTimeOffset? ArchivedAt);