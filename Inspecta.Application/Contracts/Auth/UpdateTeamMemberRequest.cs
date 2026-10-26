using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Auth;

/// <summary>Profile fields a technical manager may update for a team member.</summary>
public sealed record UpdateTeamMemberRequest
{
    [Required, MaxLength(200)]
    public required string FullName { get; init; }

    public IReadOnlyList<string> Qualifications { get; init; } = [];
    public bool IsOnLeave { get; init; }
    public DateTimeOffset? LeaveUntil { get; init; }

    [Range(0, long.MaxValue)]
    public long HourlyRatePiastres { get; init; }
}
