using System.ComponentModel.DataAnnotations;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Auth;

public sealed record CreateTeamMemberRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }

    [Required, MaxLength(200)]
    public required string FullName { get; init; }

    [Required, MinLength(12), MaxLength(256)]
    public required string Password { get; init; }

    public required UserRole Role { get; init; }
    public IReadOnlyList<string> Qualifications { get; init; } = [];
    public long HourlyRatePiastres { get; init; }
}
