using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The full names of the given users in one query (users that do not exist are left out).</summary>
    async Task<IReadOnlyDictionary<Guid, string>> GetFullNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var names = new Dictionary<Guid, string>();
        foreach (var id in ids)
            if (await GetByIdAsync(id, cancellationToken) is { } user) names[id] = user.FullName;
        return names;
    }

    Task<IReadOnlyList<AppUser>> ListAsync(
        Guid? managerId = null,
        UserRole? role = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default,
        bool includeDeleted = false);
    Task AddAsync(AppUser user, CancellationToken cancellationToken = default);
    Task AddTeamMemberAsync(
        AppUser user,
        CoordinatorEmailConfig? emailConfig,
        CancellationToken cancellationToken = default);
    Task SaveAsync(AppUser user, CancellationToken cancellationToken = default);
    Task<bool> SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);
    /// <summary>Deletes the user for good (false when not found); throws <c>user_in_use</c> (409) while other records still point at them.</summary>
    Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<bool> RotateRefreshTokenAsync(RefreshToken currentToken, RefreshToken nextToken, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenFamilyAsync(Guid familyId, CancellationToken cancellationToken = default);
    Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Signs out every device of the user except the one holding the refresh token with this hash.</summary>
    Task RevokeOtherRefreshTokensAsync(Guid userId, string keepTokenHash, CancellationToken cancellationToken = default) =>
        RevokeAllRefreshTokensAsync(userId, cancellationToken);
}
