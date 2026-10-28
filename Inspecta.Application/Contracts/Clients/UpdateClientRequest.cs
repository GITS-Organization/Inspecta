using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Clients;

/// <summary>Payload for updating a client's profile and business details.</summary>
public sealed record UpdateClientRequest
{
    [Required, MaxLength(200)]
    public required string Name { get; init; }

    public bool IsCompany { get; init; }

    public bool HasContract { get; init; }

    [MaxLength(500)]
    public string? Location { get; init; }

    [Range(-90, 90)]
    public double? Latitude { get; init; }

    [Range(-180, 180)]
    public double? Longitude { get; init; }

    [EmailAddress, MaxLength(320)]
    public string? CertificatesEmail { get; init; }

    [MaxLength(64)]
    public string? TaxNumber { get; init; }

    [MaxLength(4000)]
    public string? Notes { get; init; }
}
