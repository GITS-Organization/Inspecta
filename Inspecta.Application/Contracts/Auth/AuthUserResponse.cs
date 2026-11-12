using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Auth;

/// <summary>Safe identity, manager, subscription, role, and staff profile fields returned by the API.</summary>
public sealed record AuthUserResponse(
	Guid Id,
	string Email,
	string FullName,
	UserRole Role,
	bool IsActive,
	IReadOnlyList<string> Qualifications,
	bool IsOnLeave,
	DateTimeOffset? LeaveUntil,
	Guid? ManagerId,
	Guid? CreatedById,
	SubscriptionStatus? SubscriptionStatus,
	DateTimeOffset? SubscriptionEndDate,
	/// <summary>What the user may do (see the permission map): the screens show or hide their buttons by it.</summary>
	IReadOnlyList<string>? Permissions = null);