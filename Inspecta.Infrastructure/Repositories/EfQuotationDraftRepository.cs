using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfQuotationDraftRepository(InspectaDbContext dbContext)
    : IQuotationDraftRepository
{
    public async Task<IReadOnlyList<QuotationDraft>> ListAsync(
        Guid? tenantId,
        Guid? coordinatorId,
        CancellationToken cancellationToken = default) =>
        await ApplyScope(dbContext.QuotationDrafts.AsNoTracking().Include(draft => draft.Coordinator), tenantId, coordinatorId)
            .OrderByDescending(draft => draft.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<QuotationDraft?> GetForCoordinatorAsync(
        Guid coordinatorId,
        Guid draftId,
        bool tracking,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.QuotationDrafts.Include(draft => draft.Coordinator).AsQueryable();
        if (!tracking)
            query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(
            draft => draft.Id == draftId && draft.CoordinatorId == coordinatorId,
            cancellationToken);
    }

    private static IQueryable<QuotationDraft> ApplyScope(
        IQueryable<QuotationDraft> query,
        Guid? tenantId,
        Guid? coordinatorId)
    {
        if (tenantId is Guid scopedTenantId)
            query = query.Where(draft => draft.TenantId == scopedTenantId);
        if (coordinatorId is Guid scopedCoordinatorId)
            query = query.Where(draft => draft.CoordinatorId == scopedCoordinatorId);
        return query;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
