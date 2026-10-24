using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AppUser?> GetByFirebaseUidAsync(string firebaseUid, CancellationToken cancellationToken = default);
    Task AddAsync(AppUser user, CancellationToken cancellationToken = default);
    Task SaveAsync(AppUser user, CancellationToken cancellationToken = default);
    Task<bool> DeactivateAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<bool> RotateRefreshTokenAsync(RefreshToken currentToken, RefreshToken nextToken, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenFamilyAsync(Guid familyId, CancellationToken cancellationToken = default);
    Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default);
}
