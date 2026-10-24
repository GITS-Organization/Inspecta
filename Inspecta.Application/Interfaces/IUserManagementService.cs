using Inspecta.Application.Contracts.Auth;

namespace Inspecta.Application.Interfaces;

public interface IUserManagementService
{
    Task<AuthUserResponse> CreateAsync(CreateStaffUserRequest request, CancellationToken cancellationToken = default);
    Task<AuthUserResponse?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AuthUserResponse> UpdateProfileAsync(Guid userId, UpdateStaffProfileRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid userId, CancellationToken cancellationToken = default);
}