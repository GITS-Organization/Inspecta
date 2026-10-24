using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
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

    public Task<InspectionRequest?> GetByIdAsync(Guid requestId, bool tracking, CancellationToken cancellationToken = default)
    {
        IQueryable<InspectionRequest> query = dbContext.Requests
            .Include(item => item.Items)
            .Include(item => item.History);
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
    }

    public async Task<IReadOnlyList<InspectionRequest>> ListAsync(Guid? inspectorId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Requests.AsNoTracking()
            .Include(item => item.Items)
            .Include(item => item.History)
            .AsQueryable();
        if (inspectorId is Guid id) query = query.Where(item => item.InspectorId == id);
        return await query.OrderByDescending(item => item.ReceivedAt).ToListAsync(cancellationToken);
    }

    public Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        dbContext.Clients.AnyAsync(client => client.Id == clientId && client.ArchivedAt == null, cancellationToken);
}