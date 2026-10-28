using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfQuotationDraftRepository(InspectaDbContext dbContext)
    : IQuotationDraftRepository
{
    public async Task<IReadOnlyList<QuotationDraft>> ListForCoordinatorAsync(
        Guid coordinatorId,
        CancellationToken cancellationToken = default) =>
        await dbContext.QuotationDrafts.AsNoTracking()
            .Where(draft => draft.CoordinatorId == coordinatorId)
            .OrderByDescending(draft => draft.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<QuotationDraft?> GetForCoordinatorAsync(
        Guid coordinatorId,
        Guid draftId,
        bool tracking,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.QuotationDrafts.AsQueryable();
        if (!tracking)
            query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(
            draft => draft.Id == draftId && draft.CoordinatorId == coordinatorId,
            cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
