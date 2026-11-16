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

    public async Task RecordCheckAsync(Guid coordinatorId, DateTimeOffset expectedUpdatedAt, DateTimeOffset at, string? failure, string? step, CancellationToken cancellationToken = default)
    {
        var config = await StillSameAsync(coordinatorId, expectedUpdatedAt, cancellationToken);
        if (config is null) return;
        if (failure is null) config.RecordSuccessfulCheck(at);
        else config.RecordFailedCheck(at, failure, step ?? "reach");
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordSyncAsync(Guid coordinatorId, DateTimeOffset expectedUpdatedAt, DateTimeOffset at, string? failure, string? step, CancellationToken cancellationToken = default)
    {
        var config = await StillSameAsync(coordinatorId, expectedUpdatedAt, cancellationToken);
        if (config is null) return;
        var changed = failure is null ? config.RecordSyncSuccess(at) : config.RecordSyncFailure(at, failure, step ?? "reach");
        if (changed) await dbContext.SaveChangesAsync(cancellationToken);
    }

    // The mailbox as it is now, but only when it still has the settings the result is about (a save or a reconnect changes UpdatedAt).
    private async Task<CoordinatorEmailConfig?> StillSameAsync(Guid coordinatorId, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken)
    {
        var config = await dbContext.CoordinatorEmailConfigs.SingleOrDefaultAsync(item => item.CoordinatorId == coordinatorId, cancellationToken);
        return config is not null && config.UpdatedAt == expectedUpdatedAt ? config : null;
    }

    public async Task RecordEmailReceivedAsync(Guid coordinatorId, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var config = await dbContext.CoordinatorEmailConfigs.SingleOrDefaultAsync(item => item.CoordinatorId == coordinatorId, cancellationToken);
        if (config is null) return;
        config.RecordEmailReceived(at);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CoordinatorEmailConfig config, CancellationToken cancellationToken = default)
    {
        dbContext.CoordinatorEmailConfigs.Remove(config);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
