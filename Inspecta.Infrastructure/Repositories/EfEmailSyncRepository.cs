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
        "IX_RequestEmails_CoordinatorId_MailboxUidValidity_MailboxUid";

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

    public async Task<bool> SaveRequestIfNewAsync(
        InspectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email ?? throw new ArgumentException("The request has no source email.", nameof(request));

        // Messages already imported as quotation drafts before requests replaced them are not imported twice.
        var legacyUid = (uint)email.MailboxUid;
        if (await dbContext.QuotationDrafts.IgnoreQueryFilters().AnyAsync(
                draft => draft.CoordinatorId == email.CoordinatorId &&
                         draft.SourceUidValidity == email.MailboxUidValidity &&
                         draft.SourceUid == legacyUid,
                cancellationToken))
            return false;

        dbContext.Requests.Add(request);
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
            dbContext.ChangeTracker.Clear();
            return false;
        }
        catch
        {
            // A failed request must not stay tracked, or it would be re-saved with the next message.
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }
}
