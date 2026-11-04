using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Requests;

/// <summary>A day of an inspector's time for a registered client: no machines and no quotation, the inspector adds the machines on site.</summary>
public sealed record CreateManDayRequest
{
    /// <summary>Technical manager owner; honored only for Admin.</summary>
    public Guid? ManagerId { get; init; }

    [Required]
    public Guid ClientId { get; init; }

    [Required]
    public Guid InspectorId { get; init; }

    /// <summary>The day (and time) the inspector goes to the client.</summary>
    [Required]
    public DateTimeOffset ScheduledAt { get; init; }

    /// <summary>Where to go; the client's location when left out.</summary>
    [MaxLength(500)]
    public string? Location { get; init; }

    /// <summary>What the client asked for, and anything the inspector should know.</summary>
    [MaxLength(2000)]
    public string? Note { get; init; }
}
