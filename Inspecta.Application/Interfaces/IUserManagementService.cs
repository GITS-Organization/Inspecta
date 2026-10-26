using Inspecta.Application.Contracts.Auth;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Interfaces;

public interface IUserManagementService
{
    Task<AuthUserResponse> CreateAsync(CreateStaffUserRequest request, CancellationToken cancellationToken = default);
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
    Task<IReadOnlyList<CompanyResponse>> ListCompaniesAsync(
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);
    Task<CompanyWithManagerResponse> CreateCompanyWithManagerAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default);
    Task<CompanyResponse?> GetCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<CompanyResponse> UpdateCompanySubscriptionAsync(
        Guid companyId,
        UpdateCompanySubscriptionRequest request,
        CancellationToken cancellationToken = default);
    Task DeleteCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuthUserResponse>> ListTeamAsync(Guid managerId, CancellationToken cancellationToken = default);
    Task<AuthUserResponse> CreateTeamMemberAsync(Guid managerId, CreateTeamMemberRequest request, CancellationToken cancellationToken = default);
    Task<AuthUserResponse?> GetTeamMemberAsync(Guid managerId, Guid userId, CancellationToken cancellationToken = default);
    Task<AuthUserResponse> UpdateTeamMemberAsync(Guid managerId, Guid userId, UpdateTeamMemberRequest request, CancellationToken cancellationToken = default);
    Task SetTeamMemberActiveAsync(
        Guid managerId,
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default);
    Task DeactivateTeamMemberAsync(Guid managerId, Guid userId, CancellationToken cancellationToken = default);
}