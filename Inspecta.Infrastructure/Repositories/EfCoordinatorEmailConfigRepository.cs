using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfCoordinatorEmailConfigRepository(InspectaDbContext dbContext)
    : ICoordinatorEmailConfigRepository
{
    public Task<CoordinatorEmailConfig?> GetByCoordinatorAsync(
        Guid coordinatorId,
        CancellationToken cancellationToken = default) =>
        dbContext.CoordinatorEmailConfigs
            .SingleOrDefaultAsync(config => config.CoordinatorId == coordinatorId, cancellationToken);

    public async Task SaveAsync(
        CoordinatorEmailConfig config,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Entry(config).State == EntityState.Detached)
            dbContext.CoordinatorEmailConfigs.Add(config);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<CoordinatorEmailConfig?> GetByCoordinatorNoTrackingAsync(
        Guid coordinatorId,
        CancellationToken cancellationToken = default) =>
        dbContext.CoordinatorEmailConfigs
            .AsNoTracking()
            .SingleOrDefaultAsync(config => config.CoordinatorId == coordinatorId, cancellationToken);

    public async Task<bool> RotateRefreshTokenAsync(
        Guid coordinatorId,
        string expectedEncryptedRefreshToken,
        string newEncryptedRefreshToken,
        CancellationToken cancellationToken = default) =>
        await dbContext.CoordinatorEmailConfigs
            .Where(config => config.CoordinatorId == coordinatorId &&
                             config.AuthMode == MailboxAuthMode.MicrosoftOAuth &&
                             config.EncryptedRefreshToken == expectedEncryptedRefreshToken)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(config => config.EncryptedRefreshToken, newEncryptedRefreshToken),
                cancellationToken) > 0;

    public async Task<bool> MarkNeedsReconnectAsync(
        Guid coordinatorId,
        string expectedEncryptedRefreshToken,
        string reason,
        CancellationToken cancellationToken = default) =>
        await dbContext.CoordinatorEmailConfigs
            .Where(config => config.CoordinatorId == coordinatorId &&
                             config.AuthMode == MailboxAuthMode.MicrosoftOAuth &&
                             config.EncryptedRefreshToken == expectedEncryptedRefreshToken)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(config => config.ConnectionStatus, MailboxConnectionStatus.NeedsReconnect)
                    .SetProperty(config => config.LastAuthError, reason),
                cancellationToken) > 0;

    public async Task DeleteAsync(CoordinatorEmailConfig config, CancellationToken cancellationToken = default)
    {
        dbContext.CoordinatorEmailConfigs.Remove(config);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
