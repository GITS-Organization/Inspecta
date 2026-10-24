using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Clients;

/// <summary>Payload for creating a client and its initial contact records.</summary>
public sealed record CreateClientRequest
{
    [Required, MaxLength(200)]
    public required string Name { get; init; }

    [MaxLength(4000)]
    public string? Notes { get; init; }

    public IReadOnlyList<CreateClientContactRequest> Contacts { get; init; } = [];
}

/// <summary>Contact person belonging to a client.</summary>
public sealed record CreateClientContactRequest
{
    [Required, MaxLength(200)]
    public required string Name { get; init; }

    [EmailAddress, MaxLength(320)]
    public string? Email { get; init; }

    [MaxLength(64)]
    public string? Phone { get; init; }

    public bool IsPrimary { get; init; }
}