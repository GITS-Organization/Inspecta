using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Auth;

/// <summary>Safe identity, company, role, and staff profile fields returned by the API.</summary>
public sealed record AuthUserResponse(
	Guid Id,
	string Email,
	string FullName,
	UserRole Role,
	bool IsActive,
	IReadOnlyList<string> Qualifications,
	bool IsOnLeave,
	DateTimeOffset? LeaveUntil,
	Guid? CompanyId,
	Guid? CreatedById);