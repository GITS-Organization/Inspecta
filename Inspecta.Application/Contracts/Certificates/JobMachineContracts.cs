using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Certificates;

/// <summary>
/// Adds a machine to a job that is under way. Either a registered machine of the client (<c>equipmentId</c>) or a new one by its details:
/// a new machine is registered for the client at once (except gears), so it is in the client's list and can be certified.
/// </summary>
public sealed record AddMachineRequest
{
    /// <summary>A registered machine of the job's client that is not in the job yet.</summary>
    public Guid? EquipmentId { get; init; }

    /// <summary>The new machine's name; required when there is no <c>equipmentId</c>.</summary>
    [MaxLength(200)]
    public string? Name { get; init; }

    [Range(1, 10000)]
    public int Quantity { get; init; } = 1;

    [MaxLength(200)]
    public string? Type { get; init; }

    [MaxLength(200)]
    public string? Capacity { get; init; }

    [MaxLength(600)]
    public string? SerialNumber { get; init; }

    [MaxLength(200)]
    public string? Manufacturer { get; init; }

    [MaxLength(200)]
    public string? Model { get; init; }

    [MaxLength(500)]
    public string? Location { get; init; }

    [MaxLength(4000)]
    public string? IssueDescription { get; init; }
}

/// <summary>New details of one machine of a job that is under way.</summary>
public sealed record EditMachineRequest
{
    [Required, MaxLength(200)]
    public required string Name { get; init; }

    [Range(1, 10000)]
    public int Quantity { get; init; } = 1;

    [MaxLength(200)]
    public string? Type { get; init; }

    [MaxLength(200)]
    public string? Capacity { get; init; }

    [MaxLength(600)]
    public string? SerialNumber { get; init; }

    [MaxLength(200)]
    public string? Manufacturer { get; init; }

    [MaxLength(200)]
    public string? Model { get; init; }

    [MaxLength(500)]
    public string? Location { get; init; }

    [MaxLength(4000)]
    public string? IssueDescription { get; init; }
}
