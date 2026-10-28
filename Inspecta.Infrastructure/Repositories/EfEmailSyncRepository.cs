using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfEmailSyncRepository(InspectaDbContext dbContext) : IEmailSyncRepository
{
    private const string SourceUidIndexName =
        "IX_QuotationDrafts_CoordinatorId_SourceUidValidity_SourceUid";

    public async Task<IReadOnlyList<CoordinatorEmailConfig>> ListActiveConfigsAsync(
        CancellationToken cancellationToken = default) =>
        await (
            from config in dbContext.CoordinatorEmailConfigs.AsNoTracking()
            join coordinator in dbContext.Users.IgnoreQueryFilters().AsNoTracking()
                on config.CoordinatorId equals coordinator.Id
            join manager in dbContext.Users.IgnoreQueryFilters().AsNoTracking()
                on config.TenantId equals manager.Id
            where coordinator.Role == UserRole.Coordinator &&
                  coordinator.IsActive &&
                  !coordinator.IsDeleted &&
                  coordinator.ManagerId == manager.Id &&
                  manager.Role == UserRole.TechnicalManager &&
                  manager.IsActive &&
                  !manager.IsDeleted &&
                  manager.SubscriptionStatus == SubscriptionStatus.Active &&
                  (manager.SubscriptionEndDate == null ||
                   manager.SubscriptionEndDate > DateTimeOffset.UtcNow)
            select config)
            .ToListAsync(cancellationToken);

    public async Task<bool> SaveDraftIfNewAsync(
        QuotationDraft draft,
        CancellationToken cancellationToken = default)
    {
        dbContext.QuotationDrafts.Add(draft);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: SourceUidIndexName
            })
        {
            dbContext.Entry(draft).State = EntityState.Detached;
            return false;
        }
    }
}
