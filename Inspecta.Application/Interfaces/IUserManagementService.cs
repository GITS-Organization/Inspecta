using Inspecta.Application.Contracts.Auth;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Interfaces;

public interface IUserManagementService
{
    Task<AuthUserResponse> CreateAsync(CreateStaffUserRequest request, CancellationToken cancellationToken = default);
    Task<AuthUserResponse> CreateTechnicalManagerAsync(
        CreateTechnicalManagerRequest request,
        CancellationToken cancellationToken = default);
    Task<AuthUserResponse> UpdateTechnicalManagerSubscriptionAsync(
        Guid managerId,
        UpdateTechnicalManagerSubscriptionRequest request,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuthUserResponse>> ListAsync(
        Guid? companyId,
        UserRole? role,
        bool? isActive,
        CancellationToken cancellationToken = default,
        bool includeDeleted = false);
    Task<AuthUserResponse?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AuthUserResponse> UpdateProfileAsync(Guid userId, UpdateStaffProfileRequest request, CancellationToken cancellationToken = default);
    Task SetUserActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuthUserResponse>> ListTeamAsync(Guid managerId, CancellationToken cancellationToken = default);
    Task<AuthUserResponse> CreateTeamMemberAsync(Guid managerId, CreateTeamMemberRequest request, CancellationToken cancellationToken = default, Guid? createdById = null);
    Task<AuthUserResponse?> GetTeamMemberAsync(Guid managerId, Guid userId, CancellationToken cancellationToken = default);
    Task<AuthUserResponse> UpdateTeamMemberAsync(Guid managerId, Guid userId, UpdateTeamMemberRequest request, CancellationToken cancellationToken = default);
    Task SetTeamMemberActiveAsync(
        Guid managerId,
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default);
    Task DeactivateTeamMemberAsync(Guid managerId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>A manager sets the password of a coordinator or inspector of their own team; that person is signed out everywhere.</summary>
    Task SetTeamMemberPasswordAsync(Guid managerId, Guid userId, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>A manager corrects the sign-in email of a member of their own team (409 account_exists when it is taken).</summary>
    Task<AuthUserResponse> ChangeTeamMemberEmailAsync(Guid managerId, Guid userId, string email, CancellationToken cancellationToken = default);

    /// <summary>An admin sets anyone's password; that person is signed out everywhere.</summary>
    Task SetUserPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>An admin corrects anyone's sign-in email.</summary>
    Task<AuthUserResponse> ChangeUserEmailAsync(Guid userId, string email, CancellationToken cancellationToken = default);
}