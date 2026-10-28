using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Requests;

/// <summary>Structured fields used to simulate an incoming inspection-request email.</summary>
public sealed record TestIncomingEmailRequest
{
    /// <summary>Technical manager owner; honored only for Admin requests.</summary>
    public Guid? ManagerId { get; init; }

    [Required, MaxLength(200)]
    public required string ClientName { get; init; }

    [Required, MaxLength(500)]
    public required string Location { get; init; }

    [Required, EmailAddress, MaxLength(320)]
    public required string ContactEmail { get; init; }

    [Required, MaxLength(200)]
    public required string MachineModel { get; init; }

    [MaxLength(200)]
    public string? SerialNumber { get; init; }

    [Required, MaxLength(4000)]
    public required string IssueDescription { get; init; }
}
