using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Clients;

/// <summary>Payload for creating a client and its initial contact records.</summary>
public sealed record CreateClientRequest
{
    /// <summary>Technical manager owner; honored only for Admin requests.</summary>
    public Guid? ManagerId { get; init; }

    [Required, MaxLength(200)]
    public required string Name { get; init; }

    /// <summary>Whether this client represents a company rather than an individual.</summary>
    public bool IsCompany { get; init; }

    /// <summary>Whether this client currently has a contract.</summary>
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

    public IReadOnlyList<CreateClientContactRequest> Contacts { get; init; } = [];
}

/// <summary>Contact person belonging to a client.</summary>
public sealed record CreateClientContactRequest
{
    /// <summary>When updating a client: the id of an existing contact to keep and edit. Leave empty for a new contact.</summary>
    public Guid? Id { get; init; }

    [Required, MaxLength(200)]
    public required string Name { get; init; }

    [EmailAddress, MaxLength(320)]
    public string? Email { get; init; }

    [MaxLength(64)]
    public string? Phone { get; init; }

    public bool IsPrimary { get; init; }
}