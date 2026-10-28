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

    public async Task<IReadOnlyList<SenderRule>> ListSenderRulesAsync(
        Guid managerId,
        CancellationToken cancellationToken = default)
    {
        var rules = await dbContext.ClientMailRules.IgnoreQueryFilters().AsNoTracking()
            .Where(rule => rule.ManagerId == managerId)
            .ToListAsync(cancellationToken);
        var clientIds = rules.Where(rule => rule.ClientId != null).Select(rule => rule.ClientId!.Value).Distinct().ToList();
        var clientNames = clientIds.Count == 0
            ? []
            : await dbContext.Clients.IgnoreQueryFilters().AsNoTracking()
                .Where(client => clientIds.Contains(client.Id) && client.ManagerId == managerId && client.ArchivedAt == null)
                .ToDictionaryAsync(client => client.Id, client => client.Name, cancellationToken);

        return rules
            .Select(rule => new SenderRule(
                rule,
                rule.ClientId is Guid clientId && clientNames.TryGetValue(clientId, out var name) ? name : null))
            .ToArray();
    }

    public Task SaveSyncCursorAsync(
        Guid coordinatorId,
        string uidValidity,
        long lastUid,
        DateTimeOffset syncFromUtc,
        CancellationToken cancellationToken = default) =>
        dbContext.CoordinatorEmailConfigs
            .Where(config => config.CoordinatorId == coordinatorId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(config => config.LastUidValidity, uidValidity)
                    .SetProperty(config => config.LastUid, lastUid)
                    .SetProperty(config => config.SyncFromUtc, syncFromUtc),
                cancellationToken);

    public Task<int> CountUntouchedEmailRequestsAsync(
        DateTimeOffset createdBefore,
        Guid? managerId,
        CancellationToken cancellationToken = default) =>
        UntouchedEmailRequests(createdBefore, managerId).CountAsync(cancellationToken);

    public Task<int> DeleteUntouchedEmailRequestsAsync(
        DateTimeOffset createdBefore,
        Guid? managerId,
        CancellationToken cancellationToken = default) =>
        UntouchedEmailRequests(createdBefore, managerId).ExecuteDeleteAsync(cancellationToken);

    private IQueryable<InspectionRequest> UntouchedEmailRequests(DateTimeOffset createdBefore, Guid? managerId) =>
        dbContext.Requests.IgnoreQueryFilters().Where(request =>
            request.Source == RequestSource.Email &&
            request.Status == JobStatus.RequestReceived &&
            request.ClientName == string.Empty &&
            request.Location == string.Empty &&
            request.ClientId == null &&
            request.InspectorId == null &&
            !request.Items.Any() &&
            !request.Certificates.Any() &&
            request.CreatedAt < createdBefore &&
            (managerId == null || request.ManagerId == managerId));

    public Task<int> DeleteExpiredMaybeRequestsAsync(
        DateTimeOffset createdBefore,
        CancellationToken cancellationToken = default) =>
        dbContext.Requests.IgnoreQueryFilters()
            .Where(request => request.Triage == RequestTriage.Maybe && request.CreatedAt < createdBefore)
            .ExecuteDeleteAsync(cancellationToken);

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
