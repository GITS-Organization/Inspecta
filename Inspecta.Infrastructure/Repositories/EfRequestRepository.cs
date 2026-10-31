using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfRequestRepository(InspectaDbContext dbContext) : IRequestRepository
{
    public async Task AddAsync(InspectionRequest request, CancellationToken cancellationToken = default)
    {
        dbContext.Requests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAsync(InspectionRequest request, CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(InspectionRequest request, CancellationToken cancellationToken = default)
    {
        dbContext.Requests.Remove(request);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // New children carry client-generated keys, so they are added explicitly instead of relying on change detection.
    public async Task AddItemAsync(InspectionRequest request, RequestItem item, CancellationToken cancellationToken = default)
    {
        dbContext.RequestItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public void StageRevision(RequestItemChanges changes)
    {
        dbContext.RequestItems.AddRange(changes.Added);
        dbContext.RequestItems.RemoveRange(changes.Removed);
    }

    // New items have client-generated keys, so they are added explicitly; the request, its edited items and the
    // added and removed items are written by a single SaveChanges (one transaction).
    public async Task SaveRevisionAsync(InspectionRequest request, RequestItemChanges changes, CancellationToken cancellationToken = default)
    {
        dbContext.RequestItems.AddRange(changes.Added);
        dbContext.RequestItems.RemoveRange(changes.Removed);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveItemAsync(InspectionRequest request, RequestItem item, CancellationToken cancellationToken = default)
    {
        dbContext.RequestItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<InspectionRequest?> GetByIdAsync(Guid requestId, bool tracking, CancellationToken cancellationToken = default)
    {
        IQueryable<InspectionRequest> query = dbContext.Requests
            .Include(item => item.Items)
            .Include(item => item.Certificates)
            .Include(item => item.History)
            .Include(item => item.ReviewComments)
            .AsSplitQuery();
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
    }

    public Task<RequestEmail?> GetEmailAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        dbContext.RequestEmails.AsNoTracking()
            .SingleOrDefaultAsync(item => item.RequestId == requestId, cancellationToken);

    public async Task<IReadOnlyList<InspectionRequest>> ListAsync(RequestListFilter filter, CancellationToken cancellationToken = default)
    {
        var query = Scope(filter)
            .Include(item => item.Items)
            .Include(item => item.History)
            .AsSplitQuery();
        if (filter.Status is JobStatus status) query = query.Where(item => item.Status == status);
        if (filter.ExcludeStatuses is { Count: > 0 } excluded)
        {
            var excludedStatuses = excluded.ToArray();
            query = query.Where(item => !excludedStatuses.Contains(item.Status));
        }
        if (filter.Search is { Length: > 0 } search)
        {
            var escaped = search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = "%" + escaped + "%";
            query = query.Where(item =>
                EF.Functions.ILike(item.ClientName, pattern) ||
                EF.Functions.ILike(item.Location, pattern) ||
                (item.Subject != null && EF.Functions.ILike(item.Subject, pattern)) ||
                (item.EmailFrom != null && EF.Functions.ILike(item.EmailFrom, pattern)) ||
                (item.EmailFromName != null && EF.Functions.ILike(item.EmailFromName, pattern)));
        }

        return await query
            .OrderByDescending(item => item.ReceivedAt)
            .ThenBy(item => item.Id)
            .Skip(filter.Skip)
            .Take(filter.Take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RequestCount>> CountAsync(RequestListFilter scope, CancellationToken cancellationToken = default) =>
        (await Scope(scope with { Triage = null, Status = null })
            .GroupBy(item => new { item.Triage, item.Status })
            .Select(group => new { group.Key.Triage, group.Key.Status, Count = group.Count() })
            .ToListAsync(cancellationToken))
        .Select(item => new RequestCount(item.Triage, item.Status, item.Count))
        .ToArray();

    private IQueryable<InspectionRequest> Scope(RequestListFilter filter)
    {
        var query = dbContext.Requests.AsNoTracking().AsQueryable();
        if (filter.InspectorId is Guid inspectorId) query = query.Where(item => item.InspectorId == inspectorId);
        if (filter.CoordinatorId is Guid coordinatorId)
            query = query.Where(item => item.Source != RequestSource.Email || item.CoordinatorId == coordinatorId);
        if (filter.ManagerId is Guid managerId) query = query.Where(item => item.ManagerId == managerId);
        if (filter.Triage is RequestTriage triage) query = query.Where(item => item.Triage == triage);
        return query;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountActiveByInspectorAsync(
        IReadOnlyCollection<Guid> inspectorIds,
        CancellationToken cancellationToken = default)
    {
        var ids = inspectorIds.ToArray();
        JobStatus[] active =
            [JobStatus.Assigned, JobStatus.TaskAccepted, JobStatus.InProgress, JobStatus.CertificateSubmitted, JobStatus.CertificateReturned];
        return await dbContext.Requests.AsNoTracking()
            .Where(item => item.InspectorId != null && ids.Contains(item.InspectorId.Value) && active.Contains(item.Status))
            .GroupBy(item => item.InspectorId!.Value)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.Key, entry => entry.Count, cancellationToken);
    }

    public Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        dbContext.Clients.AnyAsync(client => client.Id == clientId && client.ArchivedAt == null, cancellationToken);

    public Task<Guid?> GetClientManagerIdAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        dbContext.Clients
            .Where(client => client.Id == clientId)
            .Select(client => client.ManagerId)
            .SingleOrDefaultAsync(cancellationToken);
}
