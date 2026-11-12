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
    ISecretProtector secretProtector,
    ICompanyProfileService? companyProfiles = null,
    ILoginAttemptTracker? loginAttempts = null,
    ICompanyRepository? companies = null,
    ITransactionRunner? transactions = null,
    IReviewerService? reviewers = null) : IUserManagementService
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
                throw new AuthException("manager_required", "A company is required for staff accounts.", 400);
            await EnsureStaffCompanyAsync(managerId.Value, cancellationToken);
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
        return await ResponseAsync(user, cancellationToken);
    }

    public async Task<AuthUserResponse> CreateTechnicalManagerAsync(
        CreateTechnicalManagerRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePassword(request.Password);
        if (request.SubscriptionMonths is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(request.SubscriptionMonths));

        // The identity is checked first: a mistake in it must not leave an account without it.
        if (request.CompanyProfile is not null)
        {
            if (companyProfiles is null) throw new InvalidOperationException("Company profiles are not available.");
            companyProfiles.Validate(request.CompanyProfile);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        await EnsureEmailAvailableAsync(email, cancellationToken);
        if (companies is null) throw new InvalidOperationException("Companies are not available.");

        // A technical manager created here starts a company that carries the manager's own id: that is how every company looked before
        // companies existed, so the screens and routes that name the company by its manager keep working. The manager is its first
        // technical manager, so the default reviewer. The company, the account and the profile are saved together or not at all.
        var manager = CreateUser(email, request.FullName, UserRole.TechnicalManager, request.Password, []);
        manager.AssignToManager(manager.Id);
        var company = Company.Create(
            request.CompanyProfile?.Name is { Length: > 0 } profileName ? profileName : request.FullName,
            DateTimeOffset.UtcNow.AddMonths(request.SubscriptionMonths),
            id: manager.Id);
        company.SetDefaultReviewer(manager.Id);

        async Task<bool> Save()
        {
            await companies.AddAsync(company, cancellationToken);
            await userRepository.AddAsync(manager, cancellationToken);
            if (request.CompanyProfile is not null)
                await companyProfiles!.SaveAsync(company.Id, request.CompanyProfile, cancellationToken);
            return true;
        }

        if (transactions is null) await Save();
        else await transactions.RunAsync(Save, cancellationToken);
        return ToResponse(manager, company);
    }

    public async Task<AuthUserResponse> UpdateTechnicalManagerSubscriptionAsync(
        Guid managerId,
        UpdateTechnicalManagerSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.SubscriptionStatus))
            throw new ArgumentException("Subscription status is invalid.", nameof(request.SubscriptionStatus));

        var company = companies is null ? null : await companies.GetByIdAsync(managerId, tracking: true, cancellationToken);
        if (company is null)
            throw new AuthException("technical_manager_not_found", "Company was not found.", 404);

        var manager = await FirstTechnicalManagerAsync(company, cancellationToken)
            ?? throw new AuthException("company_has_no_technical_manager", "The company has no technical manager yet.", 409);
        company.UpdateSubscription(request.SubscriptionStatus, request.SubscriptionEndDate);
        await companies!.SaveAsync(cancellationToken);
        return ToResponse(manager, company);
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
        return await ResponsesAsync(await userRepository.ListAsync(managerId, role, isActive, cancellationToken, includeDeleted), cancellationToken);
    }

    public async Task<AuthUserResponse?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : await ResponseAsync(user, cancellationToken);
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
        static bool Locked(UserRole role) => role is UserRole.TechnicalManager or UserRole.CompanyAdmin;
        if ((Locked(user.Role) || Locked(request.Role)) &&
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
            if (request.ManagerId is not null && request.ManagerId != user.ManagerId)
                throw new AuthException("manager_assignment_not_allowed", "A technical manager cannot be moved to another company.", 400);
            managerId = user.ManagerId;
        }
        else
        {
            managerId = request.ManagerId ?? user.ManagerId;
            if (managerId is null)
                throw new AuthException("manager_required", "A company is required for staff accounts.", 400);
            await EnsureStaffCompanyAsync(managerId.Value, cancellationToken);
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
        if (request.Role != UserRole.TechnicalManager) user.AssignToManager(managerId);
        await userRepository.SaveAsync(user, cancellationToken);
        return await ResponseAsync(user, cancellationToken);
    }

    public async Task DeactivateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AuthException("user_not_found", "User was not found.", 404);
        if (reviewers is not null) await reviewers.EnsureCanLeaveAsync(user, cancellationToken);
        if (!await userRepository.DeleteAsync(userId, cancellationToken))
            throw new AuthException("user_not_found", "User was not found.", 404);
        if (reviewers is not null) await reviewers.LeftAsync(user, cancellationToken);
    }

    public async Task SetUserActiveAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AuthException("user_not_found", "User was not found.", 404);
        if (!isActive && reviewers is not null) await reviewers.EnsureCanLeaveAsync(user, cancellationToken);
        if (!await userRepository.SetActiveAsync(userId, isActive, cancellationToken))
            throw new AuthException("user_not_found", "User was not found.", 404);
        if (!isActive && reviewers is not null) await reviewers.LeftAsync(user, cancellationToken);
    }

    public async Task<IReadOnlyList<AuthUserResponse>> ListTeamAsync(
        Guid managerId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCompanyUsableAsync(managerId, cancellationToken);
        var members = await userRepository.ListAsync(
            managerId: managerId,
            cancellationToken: cancellationToken);
        return await ResponsesAsync(members.Where(user => IsTeamRole(user.Role)), cancellationToken);
    }

    public async Task<AuthUserResponse> CreateTeamMemberAsync(
        Guid managerId,
        CreateTeamMemberRequest request,
        CancellationToken cancellationToken = default,
        Guid? createdById = null)
    {
        await EnsureCompanyUsableAsync(managerId, cancellationToken);
        if (!IsTeamRole(request.Role))
            throw new AuthException("role_forbidden", "A company can have coordinators, inspectors, technical managers, general managers and financial managers.", 403);
        if (request.Role == UserRole.Coordinator && request.EmailConfig is null)
            throw new AuthException("coordinator_email_config_required", "IMAP settings are required when creating a coordinator.", 400);
        if (request.Role != UserRole.Coordinator && request.EmailConfig is not null)
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
            managerId,
            createdById: createdById);
        var emailConfig = request.EmailConfig is null
            ? null
            : CreateEmailConfig(user.Id, managerId, request.EmailConfig);
        await userRepository.AddTeamMemberAsync(user, emailConfig, cancellationToken);
        // The first technical manager of a company is its default reviewer.
        if (request.Role == UserRole.TechnicalManager && companies is not null)
        {
            var company = await companies.GetByIdAsync(managerId, tracking: true, cancellationToken);
            if (company is { DefaultReviewerId: null })
            {
                company.SetDefaultReviewer(user.Id);
                await companies.SaveAsync(cancellationToken);
            }
        }
        return await ResponseAsync(user, cancellationToken);
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
            request.UseSsl,
            smtpServer: request.SmtpServer,
            smtpPort: request.SmtpPort,
            smtpUseSsl: request.SmtpUseSsl);
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
        await EnsureCompanyUsableAsync(managerId, cancellationToken);
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        return user is not null &&
               user.ManagerId == managerId &&
               IsTeamRole(user.Role)
            ? await ResponseAsync(user, cancellationToken)
            : null;
    }

    public async Task<AuthUserResponse> UpdateTeamMemberAsync(
        Guid managerId,
        Guid userId,
        UpdateTeamMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureCompanyUsableAsync(managerId, cancellationToken);
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
        return await ResponseAsync(user, cancellationToken);
    }

    public async Task DeactivateTeamMemberAsync(
        Guid managerId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCompanyUsableAsync(managerId, cancellationToken);
        var member = await GetScopedTeamMemberAsync(managerId, userId, cancellationToken);
        if (reviewers is not null) await reviewers.EnsureCanLeaveAsync(member, cancellationToken);
        if (!await userRepository.DeleteAsync(userId, cancellationToken))
            throw new AuthException("user_not_found", "User was not found.", 404);
        if (reviewers is not null) await reviewers.LeftAsync(member, cancellationToken);
    }

    public async Task SetTeamMemberActiveAsync(
        Guid managerId,
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await EnsureCompanyUsableAsync(managerId, cancellationToken);
        var member = await GetScopedTeamMemberAsync(managerId, userId, cancellationToken);
        if (!isActive && reviewers is not null) await reviewers.EnsureCanLeaveAsync(member, cancellationToken);
        if (!await userRepository.SetActiveAsync(userId, isActive, cancellationToken))
            throw new AuthException("user_not_found", "User was not found.", 404);
        if (!isActive && reviewers is not null) await reviewers.LeftAsync(member, cancellationToken);
    }

    public async Task SetTeamMemberPasswordAsync(Guid managerId, Guid userId, string newPassword, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyUsableAsync(managerId, cancellationToken);
        await SetPasswordAsync(await GetScopedTeamMemberAsync(managerId, userId, cancellationToken), newPassword, cancellationToken);
    }

    public async Task SetUserPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default) =>
        await SetPasswordAsync(await GetUserOrNotFoundAsync(userId, cancellationToken), newPassword, cancellationToken);

    public async Task<AuthUserResponse> ChangeTeamMemberEmailAsync(Guid managerId, Guid userId, string email, CancellationToken cancellationToken = default)
    {
        await EnsureCompanyUsableAsync(managerId, cancellationToken);
        return await ChangeEmailAsync(await GetScopedTeamMemberAsync(managerId, userId, cancellationToken), email, cancellationToken);
    }

    public async Task<AuthUserResponse> ChangeUserEmailAsync(Guid userId, string email, CancellationToken cancellationToken = default) =>
        await ChangeEmailAsync(await GetUserOrNotFoundAsync(userId, cancellationToken), email, cancellationToken);

    // A new password ends every sign-in of that person (the old password may be the thing that leaked) and lifts a lock caused by wrong guesses.
    private async Task SetPasswordAsync(AppUser user, string newPassword, CancellationToken cancellationToken)
    {
        ValidatePassword(newPassword);
        user.SetPasswordHash(passwordService.Hash(user, newPassword));
        await userRepository.SaveAsync(user, cancellationToken);
        await userRepository.RevokeAllRefreshTokensAsync(user.Id, cancellationToken);
        loginAttempts?.RecordSuccess(user.Email);
    }

    // The email is the sign-in name: it must be a real, free address, and the old sign-ins end (their tokens carry the old address).
    private async Task<AuthUserResponse> ChangeEmailAsync(AppUser user, string email, CancellationToken cancellationToken)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length is 0 or > 320 ||
            !System.Net.Mail.MailAddress.TryCreate(normalized, out var parsed) ||
            !string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase) ||
            !parsed.Host.Contains('.'))
            throw new AuthException("invalid_email", "That is not a valid email address.", 400);
        if (normalized != user.Email)
        {
            await EnsureEmailAvailableAsync(normalized, cancellationToken);
            user.ChangeEmail(normalized);
            await userRepository.SaveAsync(user, cancellationToken);
            await userRepository.RevokeAllRefreshTokensAsync(user.Id, cancellationToken);
        }

        return await ResponseAsync(user, cancellationToken);
    }

    private async Task<AppUser> GetUserOrNotFoundAsync(Guid userId, CancellationToken cancellationToken) =>
        await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AuthException("user_not_found", "User was not found.", 404);

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

    // The subscription shown for a user is the one of their company (it used to be the technical manager's own).
    private static AuthUserResponse ToResponse(AppUser user, Company? company) => new(
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
        company?.GetEffectiveSubscriptionStatus(),
        company?.SubscriptionEndDate,
        Authorization.RolePermissions.For(Authorization.RoleNames.Of(user.Role)));

    private async Task<AuthUserResponse> ResponseAsync(AppUser user, CancellationToken cancellationToken) =>
        ToResponse(user, companies is not null && user.CompanyId is { } id ? await companies.GetByIdAsync(id, tracking: false, cancellationToken) : null);

    private async Task<IReadOnlyList<AuthUserResponse>> ResponsesAsync(IEnumerable<AppUser> users, CancellationToken cancellationToken)
    {
        var list = users.ToArray();
        var found = companies is null
            ? new Dictionary<Guid, Company>()
            : await companies.GetByIdsAsync(list.Select(user => user.CompanyId).OfType<Guid>().Distinct().ToArray(), cancellationToken);
        return list.Select(user => ToResponse(user, user.CompanyId is { } id && found.TryGetValue(id, out var company) ? company : null)).ToArray();
    }

    /// <summary>The company's default reviewer, or else its oldest technical manager (the platform owner's calls answer with a user).</summary>
    private async Task<AppUser?> FirstTechnicalManagerAsync(Company company, CancellationToken cancellationToken)
    {
        if (company.DefaultReviewerId is { } reviewerId &&
            await userRepository.GetByIdAsync(reviewerId, cancellationToken) is { Role: UserRole.TechnicalManager } reviewer)
            return reviewer;
        return (await userRepository.ListAsync(company.Id, UserRole.TechnicalManager, cancellationToken: cancellationToken))
            .OrderBy(user => user.CreatedAt).FirstOrDefault();
    }

    // Team calls (a company admin or the platform owner on behalf of a company): the company must be on and its subscription valid.
    private async Task EnsureCompanyUsableAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (companies is null) return;
        var company = await companies.GetByIdAsync(companyId, tracking: false, cancellationToken);
        if (company is null || !company.IsActive)
            throw new AuthException("forbidden", "An active company account is required.", 403);
        if (!company.HasActiveSubscription())
            throw new AuthException("subscription_inactive", "The company subscription is suspended or expired.", 403);
    }

    // Creating or moving staff: the company named must exist, be on, and have a valid subscription.
    private async Task EnsureStaffCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (companies is null) return;
        if (await companies.GetByIdAsync(companyId, tracking: false, cancellationToken) is not { } company || !company.IsUsable())
            throw new AuthException("manager_invalid", "An active company with an active subscription is required.", 400);
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

    // The accounts a company manages itself: everyone but the platform owner and the company admin (only the platform owner hands those out).
    private static bool IsTeamRole(UserRole role) =>
        role is UserRole.Coordinator or UserRole.Inspector or UserRole.TechnicalManager or UserRole.GeneralManager or UserRole.FinancialManager;
}
