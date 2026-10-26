using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Auth;

public sealed record CreateCompanyRequest
{
    [Required, MaxLength(200)]
    public required string Name { get; init; }

    [Required, EmailAddress, MaxLength(320)]
    public required string ManagerEmail { get; init; }

    [Required, MaxLength(200)]
    public required string ManagerFullName { get; init; }

    [Required, MinLength(12), MaxLength(256)]
    public required string ManagerPassword { get; init; }

    [MaxLength(128)]
    public string? ManagerFirebaseUid { get; init; }
}
