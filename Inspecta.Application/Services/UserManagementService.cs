using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class UserManagementService(
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    IPasswordService passwordService) : IUserManagementService
{
    public async Task<AuthUserResponse> CreateAsync(CreateStaffUserRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Role)) throw new ArgumentException("User role is invalid.", nameof(request.Role));
        var email = request.Email.Trim().ToLowerInvariant();
        await EnsureEmailAvailableAsync(email, cancellationToken);
        if (request.HourlyRatePiastres < 0) throw new ArgumentOutOfRangeException(nameof(request.HourlyRatePiastres));
        if (request.Role != UserRole.Admin &&
            (request.CompanyId is null || !await companyRepository.ExistsAsync(request.CompanyId.Value, cancellationToken)))
            throw new AuthException("company_required", "A valid company is required for non-admin users.", 400);
        if (request.Role == UserRole.Admin && request.CompanyId is not null)
            throw new AuthException("admin_company_not_allowed", "Admin accounts cannot be assigned to a company.", 400);

        var user = CreateUser(
            email,
            request.FullName,
            request.Role,
            request.Password,
            request.Qualifications,
            request.HourlyRatePiastres,
            request.CompanyId);
        await userRepository.AddAsync(user, cancellationToken);
        return ToResponse(user);
    }

    public async Task<IReadOnlyList<AuthUserResponse>> ListAsync(
        Guid? companyId,
        UserRole? role,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        if (role is not null && !Enum.IsDefined(role.Value))
            throw new ArgumentOutOfRangeException(nameof(role));
        return (await userRepository.ListAsync(companyId, role, isActive, cancellationToken))
            .Select(ToResponse)
            .ToArray();
    }

    public async Task<AuthUserResponse?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : ToResponse(user);
    }

    public async Task<AuthUserResponse> UpdateProfileAsync(Guid userId, UpdateStaffProfileRequest request, CancellationToken cancellationToken = default)
    {
        if (request.HourlyRatePiastres < 0) throw new ArgumentOutOfRangeException(nameof(request.HourlyRatePiastres));
        if (!Enum.IsDefined(request.Role)) throw new ArgumentException("User role is invalid.", nameof(request.Role));
        if (request.IsOnLeave && request.LeaveUntil is not null && request.LeaveUntil <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Leave end date must be in the future.", nameof(request.LeaveUntil));
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AuthException("user_not_found", "User was not found.", 404);
        var companyId = request.Role == UserRole.Admin
            ? null
            : request.CompanyId ?? user.CompanyId;
        if (request.Role == UserRole.Admin)
        {
            if (request.CompanyId is not null)
                throw new AuthException("admin_company_not_allowed", "Admin accounts cannot be assigned to a company.", 400);
        }
        else if (companyId is null || !await companyRepository.ExistsAsync(companyId.Value, cancellationToken))
        {
            throw new AuthException("company_required", "A valid company is required for non-admin users.", 400);
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
            request.HourlyRatePiastres,
            user.SignatureImageId);
        user.AssignToCompany(companyId);
        await userRepository.SaveAsync(user, cancellationToken);
        return ToResponse(user);
    }

    public async Task DeactivateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await userRepository.DeactivateAsync(userId, cancellationToken))
        {
            throw new AuthException("user_not_found", "User was not found or is already inactive.", 404);
        }
    }

    public async Task<CompanyWithManagerResponse> CreateCompanyWithManagerAsync(
        CreateCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePassword(request.ManagerPassword);
        var email = request.ManagerEmail.Trim().ToLowerInvariant();
        await EnsureEmailAvailableAsync(email, cancellationToken);
        var company = Company.Create(request.Name);
        var manager = CreateUser(
            email,
            request.ManagerFullName,
            UserRole.TechnicalManager,
            request.ManagerPassword,
            [],
            0,
            company.Id);
        await companyRepository.AddWithManagerAsync(company, manager, cancellationToken);
        return new CompanyWithManagerResponse(
            ToCompanyResponse(company),
            ToResponse(manager));
    }

    public async Task<IReadOnlyList<CompanyResponse>> ListCompaniesAsync(
        CancellationToken cancellationToken = default) =>
        (await companyRepository.ListAsync(cancellationToken))
            .Select(ToCompanyResponse)
            .ToArray();

    public async Task<IReadOnlyList<AuthUserResponse>> ListTeamAsync(
        Guid managerId,
        CancellationToken cancellationToken = default)
    {
        var manager = await GetAuthorizedManagerAsync(managerId, cancellationToken);
        var members = await userRepository.ListAsync(
            companyId: manager.CompanyId,
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
        ValidatePassword(request.Password);
        if (request.HourlyRatePiastres < 0)
            throw new ArgumentOutOfRangeException(nameof(request.HourlyRatePiastres));

        var email = request.Email.Trim().ToLowerInvariant();
        await EnsureEmailAvailableAsync(email, cancellationToken);
        var user = CreateUser(
            email,
            request.FullName,
            request.Role,
            request.Password,
            request.Qualifications,
            request.HourlyRatePiastres,
            manager.CompanyId);
        await userRepository.AddAsync(user, cancellationToken);
        return ToResponse(user);
    }

        public async Task<AuthUserResponse?> GetTeamMemberAsync(
            Guid managerId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var manager = await GetAuthorizedManagerAsync(managerId, cancellationToken);
            var user = await userRepository.GetByIdAsync(userId, cancellationToken);
            return user is not null &&
                   user.CompanyId == manager.CompanyId &&
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
            var manager = await GetAuthorizedManagerAsync(managerId, cancellationToken);
            var user = await GetScopedTeamMemberAsync(manager.CompanyId!.Value, userId, cancellationToken);
            if (request.HourlyRatePiastres < 0)
                throw new ArgumentOutOfRangeException(nameof(request.HourlyRatePiastres));
            if (request.IsOnLeave && request.LeaveUntil is not null && request.LeaveUntil <= DateTimeOffset.UtcNow)
                throw new ArgumentException("Leave end date must be in the future.", nameof(request.LeaveUntil));

            user.UpdateProfile(
                request.FullName,
                user.Phone,
                CreateQualifications(request.Qualifications),
                user.Role,
                request.IsOnLeave,
                request.LeaveUntil,
                request.HourlyRatePiastres,
                user.SignatureImageId);
            await userRepository.SaveAsync(user, cancellationToken);
            return ToResponse(user);
        }

        public async Task DeactivateTeamMemberAsync(
            Guid managerId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var manager = await GetAuthorizedManagerAsync(managerId, cancellationToken);
            await GetScopedTeamMemberAsync(manager.CompanyId!.Value, userId, cancellationToken);
            if (!await userRepository.DeactivateAsync(userId, cancellationToken))
                throw new AuthException("user_not_found", "User was not found or is already inactive.", 404);
        }

    public static void ValidatePassword(string password)
    {
        if (password.Length < 12 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new AuthException("password_policy_failed", "Password must be at least 12 characters and include uppercase, lowercase, numeric, and symbol characters.", 400);
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
        user.HourlyRatePiastres,
        user.CompanyId);

    private async Task<AppUser> GetAuthorizedManagerAsync(Guid managerId, CancellationToken cancellationToken)
    {
        var manager = await userRepository.GetByIdAsync(managerId, cancellationToken);
        if (manager is null || !manager.IsActive || manager.Role != UserRole.TechnicalManager)
            throw new AuthException("forbidden", "An active technical manager account is required.", 403);
        if (manager.CompanyId is null)
            throw new AuthException("company_not_assigned", "Technical manager is not assigned to a company.", 403);
        return manager;
    }

    private async Task<AppUser> GetScopedTeamMemberAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.CompanyId != companyId || !IsTeamRole(user.Role))
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
        long hourlyRatePiastres,
        Guid? companyId)
    {
        ValidatePassword(password);
        var user = AppUser.Create(
            email,
            fullName,
            role,
            qualifications: CreateQualifications(qualificationNames),
            hourlyRatePiastres: hourlyRatePiastres,
            companyId: companyId);
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

    private static CompanyResponse ToCompanyResponse(Company company) =>
        new(company.Id, company.Name, company.IsActive, company.CreatedAt);
}