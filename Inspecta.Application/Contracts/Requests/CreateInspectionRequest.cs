using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Requests;

/// <summary>Payload for creating an inspection request from an email or coordinator entry.</summary>
public sealed record CreateInspectionRequest
{
    public Guid? ClientId { get; init; }

    /// <summary>Technical manager owner; honored only for Admin requests.</summary>
    public Guid? ManagerId { get; init; }

    [Required, MaxLength(200)]
    public required string ClientName { get; init; }

    [Required, MaxLength(500)]
    public required string Location { get; init; }

    [MaxLength(500)]
    public string? Subject { get; init; }

    [MaxLength(256)]
    public string? EmailThreadId { get; init; }

    public DateTimeOffset? PreferredDate { get; init; }
    public string? AccessNotes { get; init; }
    public IReadOnlyList<CreateRequestItem> Items { get; init; } = [];
}

/// <summary>One item requested for inspection.</summary>
public sealed record CreateRequestItem
{
    [Required, MaxLength(200)]
    public required string Name { get; init; }

    [Range(1, 10000)]
    public int Quantity { get; init; } = 1;

    public Guid? EquipmentId { get; init; }
    public string? Type { get; init; }
    public string? Capacity { get; init; }
    public string? SerialNumber { get; init; }
    [MaxLength(4000)]
    public string? IssueDescription { get; init; }
}