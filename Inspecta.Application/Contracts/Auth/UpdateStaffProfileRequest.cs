using System.ComponentModel.DataAnnotations;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Auth;

/// <summary>Admin changes to a staff member's role, manager, and operational profile.</summary>
public sealed record UpdateStaffProfileRequest
{
    [Required, MaxLength(200)]
    public required string FullName { get; init; }

    public required UserRole Role { get; init; }
    public Guid? ManagerId { get; init; }
    public IReadOnlyList<string> Qualifications { get; init; } = [];
    public bool IsOnLeave { get; init; }
    public DateTimeOffset? LeaveUntil { get; init; }
}