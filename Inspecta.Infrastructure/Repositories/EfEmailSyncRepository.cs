using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfEmailSyncRepository(
    InspectaDbContext dbContext,
    ILogger<EfEmailSyncRepository>? logger = null) : IEmailSyncRepository
{
    private const string SourceUidIndexName =
        "IX_RequestEmails_CoordinatorId_MailboxUidValidity_MailboxUid";

    public async Task<IReadOnlyList<CoordinatorEmailConfig>> ListActiveConfigsAsync(
        CancellationToken cancellationToken = default) =>
        await (
            from config in dbContext.CoordinatorEmailConfigs.AsNoTracking()
            join coordinator in dbContext.Users.IgnoreQueryFilters().AsNoTracking()
                on config.CoordinatorId equals coordinator.Id
            join company in dbContext.Companies.AsNoTracking()
                on config.TenantId equals company.Id
            where config.ConnectionStatus == MailboxConnectionStatus.Connected &&
                  coordinator.Role == UserRole.Coordinator &&
                  coordinator.IsActive &&
                  !coordinator.IsDeleted &&
                  coordinator.ManagerId == company.Id &&
                  company.IsActive &&
                  company.SubscriptionStatus == SubscriptionStatus.Active &&
                  (company.SubscriptionEndDate == null ||
                   company.SubscriptionEndDate > DateTimeOffset.UtcNow)
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
                .ToDictionaryAsync(client => client.Id, client => (client.Name, client.Location), cancellationToken);

        var senders = rules
            .Select(rule => rule.ClientId is Guid clientId && clientNames.TryGetValue(clientId, out var client)
                ? new SenderRule(rule, client.Name, client.Location)
                : new SenderRule(rule, null))
            .ToList();

        // A contact's email or the certificates email of a client is a sender of that client too, even if nobody
        // listed it as a rule. They come after the explicit rules, so a rule always wins.
        try
        {
            // Plain projections (no entity graph): the same two small queries on every cycle.
            var clients = await dbContext.Clients.IgnoreQueryFilters().AsNoTracking()
                .Where(client => client.ManagerId == managerId && client.ArchivedAt == null)
                .Select(client => new { client.Id, client.Name, client.Location, client.CertificatesEmail })
                .ToListAsync(cancellationToken);
            var ids = clients.Select(client => client.Id).ToList();
            var contacts = ids.Count == 0
                ? []
                : await dbContext.ClientContacts.IgnoreQueryFilters().AsNoTracking()
                    .Where(contact => ids.Contains(contact.ClientId) && contact.Email != null)
                    .Select(contact => new { contact.ClientId, contact.Email })
                    .ToListAsync(cancellationToken);

            var known = senders.Select(sender => sender.Rule.Pattern).ToHashSet(StringComparer.Ordinal);
            foreach (var client in clients)
            {
                var addresses = contacts.Where(contact => contact.ClientId == client.Id).Select(contact => contact.Email).Append(client.CertificatesEmail);
                foreach (var address in addresses)
                {
                    if (string.IsNullOrWhiteSpace(address)) continue;
                    ClientMailRule implied;
                    try { implied = ClientMailRule.ForKnownAddress(managerId, address, client.Id); }
                    catch (ArgumentException) { continue; }
                    if (known.Add(implied.Pattern)) senders.Add(new SenderRule(implied, client.Name, client.Location));
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Only an extra: the registered senders keep working if the clients cannot be read.
            logger?.LogWarning(exception, "Could not read the client emails of team {ManagerId}; using the registered senders only.", managerId);
        }

        return senders;
    }

    public async Task<int> PromoteMatchingMaybesAsync(
        Guid managerId,
        IReadOnlyList<SenderRule> senders,
        Func<InspectionRequest, Task<IReadOnlyList<RequestItem>>>? autofill = null,
        CancellationToken cancellationToken = default)
    {
        var known = senders.Where(sender => sender.Rule.ClientId is not null && !string.IsNullOrWhiteSpace(sender.ClientName)).ToList();
        if (known.Count == 0) return 0;

        // First only who wrote each waiting email (the bodies can be large), then load the ones that belong to a client.
        var waiting = await dbContext.Requests.IgnoreQueryFilters().AsNoTracking()
            .Where(request => request.ManagerId == managerId && request.Triage == RequestTriage.Maybe && request.EmailFrom != null)
            .Select(request => new { request.Id, request.EmailFrom })
            .ToListAsync(cancellationToken);
        var rules = known.Select(sender => sender.Rule).ToList();
        var matches = waiting
            .Select(request => (request.Id, Rule: ClientMailRule.FindMatch(rules, request.EmailFrom)))
            .Where(match => match.Rule is not null)
            .ToList();
        if (matches.Count == 0) return 0;

        var ids = matches.Select(match => match.Id).ToList();
        var requests = await dbContext.Requests.IgnoreQueryFilters()
            .Include(request => request.Email)
            .Include(request => request.Items)
            .Where(request => ids.Contains(request.Id))
            .ToListAsync(cancellationToken);
        foreach (var request in requests)
        {
            var rule = matches.First(match => match.Id == request.Id).Rule!;
            var sender = known.First(item => item.Rule.Id == rule.Id);
            request.AdoptClient(rule.ClientId!.Value, sender.ClientName!, sender.ClientLocation);
            if (autofill is null) continue;
            try
            {
                // New items carry their own ids, so they are added explicitly.
                dbContext.RequestItems.AddRange(await autofill(request));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger?.LogWarning(exception, "Could not read the machines of request {RequestId}; moved without them.", request.Id);
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
        }

        return requests.Count;
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
