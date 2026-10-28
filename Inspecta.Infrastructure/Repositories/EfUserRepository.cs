using Inspecta.Application.Interfaces;
using Inspecta.Application.Exceptions;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfUserRepository(InspectaDbContext dbContext) : IUserRepository
{
    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        dbContext.Users.IgnoreQueryFilters().SingleOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AppUser>> ListAsync(
        Guid? managerId = null,
        UserRole? role = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default,
        bool includeDeleted = false)
    {
        var query = includeDeleted
            ? dbContext.Users.IgnoreQueryFilters().AsNoTracking()
            : dbContext.Users.AsNoTracking();
        if (managerId is not null) query = query.Where(user => user.ManagerId == managerId);
        if (role is not null) query = query.Where(user => user.Role == role);
        if (isActive is not null) query = query.Where(user => user.IsActive == isActive);
        return await query.OrderBy(user => user.FullName).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTeamMemberAsync(
        AppUser user,
        CoordinatorEmailConfig? emailConfig,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Users.Add(user);
        if (emailConfig is not null)
            dbContext.CoordinatorEmailConfigs.Add(emailConfig);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (GetUniqueConstraint(exception) is { } constraint)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw constraint switch
            {
                "IX_Users_Email" => new AuthException(
                    "account_exists",
                    "Email is already registered. Email addresses must be unique across the platform.",
                    409),
                "PK_CoordinatorEmailConfigs" => new AuthException(
                    "coordinator_email_config_exists",
                    "This coordinator already has an IMAP configuration.",
                    409),
                _ => exception
            };
        }
    }

    private static string? GetUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        } postgresException
            ? postgresException.ConstraintName
            : null;

    public async Task SaveAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        if (dbContext.Entry(user).State == EntityState.Detached)
        {
            dbContext.Users.Update(user);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> SetActiveAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (isActive)
            user.Activate();
        else
            user.Deactivate();

        if (!isActive)
        {
            var tokens = await dbContext.RefreshTokens
                .Where(token => token.UserId == userId && token.RevokedAt == null)
                .ToListAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            foreach (var token in tokens) token.RevokedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SoftDeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await dbContext.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is null || user.IsDeleted)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        user.SoftDelete();
        var tokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var token in tokens) token.RevokedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task SaveRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        dbContext.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task<bool> RotateRefreshTokenAsync(
        RefreshToken currentToken,
        RefreshToken nextToken,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var updated = await dbContext.RefreshTokens
            .Where(token => token.Id == currentToken.Id && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(token => token.RevokedAt, now)
                .SetProperty(token => token.ReplacedByTokenHash, nextToken.TokenHash), cancellationToken);

        if (updated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        dbContext.RefreshTokens.Add(nextToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task RevokeRefreshTokenAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        if (refreshToken.RevokedAt is null)
        {
            refreshToken.RevokedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeRefreshTokenFamilyAsync(Guid familyId, CancellationToken cancellationToken = default)
    {
        var tokens = await dbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var token in tokens) token.RevokedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var token in tokens) token.RevokedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}