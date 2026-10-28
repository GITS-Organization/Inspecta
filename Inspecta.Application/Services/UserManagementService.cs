using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Contracts.Email;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class UserManagementService(
    IUserRepository userRepository,
    IPasswordService passwordService,
    ISecretProtector secretProtector) : IUserManagementService
{
    public async Task<AuthUserResponse> CreateAsync(
        CreateStaffUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Role))
            throw new ArgumentException("User role is invalid.", nameof(request.Role));
        if (request.Role == UserRole.TechnicalManager)
            throw new AuthException(
                "technical_manager_provisioning_required",
                "Create technical managers through the technical manager endpoint.",
                400);
        if (request.Role == UserRole.Coordinator && request.EmailConfig is null)
            throw new AuthException("coordinator_email_config_required", "IMAP settings are required when creating a coordinator.", 400);
        if (request.Role != UserRole.Coordinator && request.EmailConfig is not null)
            throw new AuthException("email_config_coordinator_only", "IMAP settings can only be assigned to coordinators.", 400);

        var managerId = request.ManagerId;
        if (request.Role == UserRole.Admin)
        {
            if (managerId is not null)
                throw new AuthException("admin_manager_not_allowed", "Admin accounts cannot be assigned to a manager.", 400);
        }
        else
        {
            if (managerId is null)
                throw new AuthException("manager_required", "A technical manager is required for staff accounts.", 400);
            await GetActiveSubscribedManagerAsync(managerId.Value, cancellationToken);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        await EnsureEmailAvailableAsync(email, cancellationToken);
        var user = CreateUser(
            email,
            request.FullName,
            request.Role,
            request.Password,
            request.Qualifications,
            managerId);
        var emailConfig = request.EmailConfig is null
            ? null
            : CreateEmailConfig(user.Id, managerId!.Value, request.EmailConfig);
        if (emailConfig is null)
            await userRepository.AddAsync(user, cancellationToken);
        else
            await userRepository.AddTeamMemberAsync(user, emailConfig, cancellationToken);
        return ToResponse(user);
    }

    public async Task<AuthUserResponse> CreateTechnicalManagerAsync(
        CreateTechnicalManagerRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePassword(request.Password);
        if (request.SubscriptionMonths is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(request.SubscriptionMonths));

        var email = request.Email.Trim().ToLowerInvariant();
        await EnsureEmailAvailableAsync(email, cancellationToken);
        var manager = CreateUser(
            email,
            request.FullName,
            UserRole.TechnicalManager,
            request.Password,
            [],
            subscriptionEndDate: DateTimeOffset.UtcNow.AddMonths(request.SubscriptionMonths));
        await userRepository.AddAsync(manager, cancellationToken);
        return ToResponse(manager);
    }

    public async Task<AuthUserResponse> UpdateTechnicalManagerSubscriptionAsync(
        Guid managerId,
        UpdateTechnicalManagerSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.SubscriptionStatus))
            throw new ArgumentException("Subscription status is invalid.", nameof(request.SubscriptionStatus));

        var manager = await userRepository.GetByIdAsync(managerId, cancellationToken);
        if (manager is null || manager.Role != UserRole.TechnicalManager)
            throw new AuthException("technical_manager_not_found", "Technical manager was not found.", 404);

        manager.UpdateSubscription(request.SubscriptionStatus, request.SubscriptionEndDate);
        await userRepository.SaveAsync(manager, cancellationToken);
        return ToResponse(manager);
    }

    public async Task<IReadOnlyList<AuthUserResponse>> ListAsync(
        Guid? managerId,
        UserRole? role,
        bool? isActive,
        CancellationToken cancellationToken = default,
        bool includeDeleted = false)
    {
        if (role is not null && !Enum.IsDefined(role.Value))
            throw new ArgumentOutOfRangeException(nameof(role));
        return (await userRepository.ListAsync(managerId, role, isActive, cancellationToken, includeDeleted))
            .Select(ToResponse)
            .ToArray();
    }

    public async Task<AuthUserResponse?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : ToResponse(user);
    }

    public async Task<AuthUserResponse> UpdateProfileAsync(
        Guid userId,
        UpdateStaffProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Role))
            throw new ArgumentException("User role is invalid.", nameof(request.Role));
        if (request.IsOnLeave && request.LeaveUntil is not null && request.LeaveUntil <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Leave end date must be in the future.", nameof(request.LeaveUntil));

        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AuthException("user_not_found", "User was not found.", 404);
        if ((user.Role == UserRole.TechnicalManager || request.Role == UserRole.TechnicalManager) &&
            user.Role != request.Role)
        {
            throw new AuthException(
                "technical_manager_role_immutable",
                "Technical manager accounts cannot be converted to or from another role.",
                400);
        }

        Guid? managerId;
        if (request.Role == UserRole.Admin)
        {
            if (request.ManagerId is not null)
                throw new AuthException("admin_manager_not_allowed", "Admin accounts cannot be assigned to a manager.", 400);
            managerId = null;
        }
        else if (request.Role == UserRole.TechnicalManager)
        {
            if (request.ManagerId is not null)
                throw new AuthException("manager_assignment_not_allowed", "Technical managers cannot be assigned to another manager.", 400);
            managerId = null;
        }
        else
        {
            managerId = request.ManagerId ?? user.ManagerId;
            if (managerId is null)
                throw new AuthException("manager_required", "A technical manager is required for staff accounts.", 400);
            await GetActiveSubscribedManagerAsync(managerId.Value, cancellationToken);
        }

        var qualifications = request.Qualifications
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => UserQualification.Create(value))
            .ToArray();
        user.UpdateProfile(
            request.FullName,
            user.Phone,
            qualifications,
            request.Role,
            request.IsOnLeave,
            request.LeaveUntil,
            user.SignatureImageId);
        user.AssignToManager(managerId);
        await userRepository.SaveAsync(user, cancellationToken);
        return ToResponse(user);
    }

    public async Task DeactivateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await userRepository.SoftDeleteAsync(userId, cancellationToken))
            throw new AuthException("user_not_found", "User was not found or is already deleted.", 404);
    }

    public async Task SetUserActiveAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (!await userRepository.SetActiveAsync(userId, isActive, cancellationToken))
            throw new AuthException("user_not_found", "User was not found.", 404);
    }

    public async Task<IReadOnlyList<AuthUserResponse>> ListTeamAsync(
        Guid managerId,
        CancellationToken cancellationToken = default)
    {
        var manager = await GetAuthorizedManagerAsync(managerId, cancellationToken);
        var members = await userRepository.ListAsync(
            managerId: manager.Id,
            cancellationToken: cancellationToken);
        return members
            .Where(user => IsTeamRole(user.Role))
            .Select(ToResponse)
            .ToArray();
    }

    public async Task<AuthUserResponse> CreateTeamMemberAsync(
        Guid managerId,
        CreateTeamMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var manager = await GetAuthorizedManagerAsync(managerId, cancellationToken);
        if (!IsTeamRole(request.Role))
            throw new AuthException("role_forbidden", "Technical managers may only create coordinators and inspectors.", 403);
        if (request.Role == UserRole.Coordinator && request.EmailConfig is null)
            throw new AuthException("coordinator_email_config_required", "IMAP settings are required when creating a coordinator.", 400);
        if (request.Role == UserRole.Inspector && request.EmailConfig is not null)
            throw new AuthException("email_config_coordinator_only", "IMAP settings can only be assigned to coordinators.", 400);
        ValidatePassword(request.Password);

        var email = request.Email.Trim().ToLowerInvariant();
        await EnsureEmailAvailableAsync(email, cancellationToken);
        var user = CreateUser(
            email,
            request.FullName,
            request.Role,
            request.Password,
            request.Qualifications,
            manager.Id,
            createdById: manager.Id);
        var emailConfig = request.EmailConfig is null
            ? null
            : CreateEmailConfig(user.Id, manager.Id, request.EmailConfig);
        await userRepository.AddTeamMemberAsync(user, emailConfig, cancellationToken);
        return ToResponse(user);
    }

    private CoordinatorEmailConfig CreateEmailConfig(
        Guid coordinatorId,
        Guid tenantId,
        CoordinatorEmailConfigRequest request)
    {
        ValidateEmailConfig(request);
        return CoordinatorEmailConfig.Create(
            coordinatorId,
            tenantId,
            request.ImapServer,
            request.ImapPort,
            request.EmailUsername,
            secretProtector.Protect(request.Password),
            request.UseSsl);
    }

    private static void ValidateEmailConfig(CoordinatorEmailConfigRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ImapServer) ||
            request.ImapServer.Length > 255 ||
            request.ImapPort is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(request.EmailUsername) ||
            request.EmailUsername.Length > 320 ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length > 256)
        {
            throw new ArgumentException("IMAP server, valid port, username, and password are required.");
        }
    }

    public async Task<AuthUserResponse?> GetTeamMemberAsync(
        Guid managerId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await GetAuthorizedManagerAsync(managerId, cancellationToken);
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        return user is not null &&
               user.ManagerId == managerId &&
               IsTeamRole(user.Role)
            ? ToResponse(user)
            : null;
    }

    public async Task<AuthUserResponse> UpdateTeamMemberAsync(
        Guid managerId,
        Guid userId,
        UpdateTeamMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        await GetAuthorizedManagerAsync(managerId, cancellationToken);
        var user = await GetScopedTeamMemberAsync(managerId, userId, cancellationToken);
        if (request.IsOnLeave && request.LeaveUntil is not null && request.LeaveUntil <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Leave end date must be in the future.", nameof(request.LeaveUntil));

        user.UpdateProfile(
            request.FullName,
            user.Phone,
            CreateQualifications(request.Qualifications),
            user.Role,
            request.IsOnLeave,
            request.LeaveUntil,
            user.SignatureImageId);
        await userRepository.SaveAsync(user, cancellationToken);
        return ToResponse(user);
    }

    public async Task DeactivateTeamMemberAsync(
        Guid managerId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await GetAuthorizedManagerAsync(managerId, cancellationToken);
        await GetScopedTeamMemberAsync(managerId, userId, cancellationToken);
        if (!await userRepository.SoftDeleteAsync(userId, cancellationToken))
            throw new AuthException("user_not_found", "User was not found or is already deleted.", 404);
    }

    public async Task SetTeamMemberActiveAsync(
        Guid managerId,
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await GetAuthorizedManagerAsync(managerId, cancellationToken);
        await GetScopedTeamMemberAsync(managerId, userId, cancellationToken);
        if (!await userRepository.SetActiveAsync(userId, isActive, cancellationToken))
            throw new AuthException("user_not_found", "User was not found.", 404);
    }

    public static void ValidatePassword(string password)
    {
        if (password.Length < 12 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new AuthException(
                "password_policy_failed",
                "Password must be at least 12 characters and include uppercase, lowercase, numeric, and symbol characters.",
                400);
        }
    }

    private static AuthUserResponse ToResponse(AppUser user) => new(
        user.Id,
        user.Email,
        user.FullName,
        user.Role,
        user.IsActive,
        user.Qualifications.Select(qualification => qualification.Name).ToArray(),
        user.IsOnLeave,
        user.LeaveUntil,
        user.ManagerId,
        user.CreatedById,
        user.GetEffectiveSubscriptionStatus(),
        user.SubscriptionEndDate);

    private async Task<AppUser> GetAuthorizedManagerAsync(Guid managerId, CancellationToken cancellationToken)
    {
        var manager = await userRepository.GetByIdAsync(managerId, cancellationToken);
        if (manager is null || !manager.IsActive || manager.Role != UserRole.TechnicalManager)
            throw new AuthException("forbidden", "An active technical manager account is required.", 403);
        if (!manager.HasActiveSubscription())
            throw new AuthException("subscription_inactive", "The technical manager subscription is suspended or expired.", 403);
        return manager;
    }

    private async Task<AppUser> GetActiveSubscribedManagerAsync(Guid managerId, CancellationToken cancellationToken)
    {
        var manager = await userRepository.GetByIdAsync(managerId, cancellationToken);
        if (manager is null || !manager.IsActive || manager.Role != UserRole.TechnicalManager ||
            !manager.HasActiveSubscription())
        {
            throw new AuthException(
                "manager_invalid",
                "An active technical manager with an active subscription is required.",
                400);
        }
        return manager;
    }

    private async Task<AppUser> GetScopedTeamMemberAsync(
        Guid managerId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.ManagerId != managerId || !IsTeamRole(user.Role))
            throw new AuthException("user_not_found", "User was not found.", 404);
        return user;
    }

    private async Task EnsureEmailAvailableAsync(string email, CancellationToken cancellationToken)
    {
        if (await userRepository.GetByEmailAsync(email, cancellationToken) is not null)
            throw new AuthException("account_exists", "Email is already registered.", 409);
    }

    private AppUser CreateUser(
        string email,
        string fullName,
        UserRole role,
        string password,
        IEnumerable<string> qualificationNames,
        Guid? managerId = null,
        Guid? createdById = null,
        DateTimeOffset? subscriptionEndDate = null)
    {
        ValidatePassword(password);
        var user = AppUser.Create(
            email,
            fullName,
            role,
            qualifications: CreateQualifications(qualificationNames),
            managerId: managerId,
            subscriptionEndDate: subscriptionEndDate,
            createdById: createdById);
        user.SetPasswordHash(passwordService.Hash(user, password));
        return user;
    }

    private static UserQualification[] CreateQualifications(IEnumerable<string> qualificationNames) =>
        (qualificationNames ?? throw new ArgumentNullException(nameof(qualificationNames)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(value => UserQualification.Create(value))
            .ToArray();

    private static bool IsTeamRole(UserRole role) =>
        role is UserRole.Coordinator or UserRole.Inspector;
}
