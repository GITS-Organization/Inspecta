using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Auth;

/// <summary>Safe identity and staff profile fields returned to an authenticated client.</summary>
public sealed record AuthUserResponse(
	Guid Id,
	string? FirebaseUid,
	string Email,
	string FullName,
	UserRole Role,
	bool IsActive,
	IReadOnlyList<string> Qualifications,
	bool IsOnLeave,
	DateTimeOffset? LeaveUntil,
	long HourlyRatePiastres,
	Guid? CompanyId);