using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfClientRepository(InspectaDbContext dbContext) : IClientRepository
{
    public async Task AddAsync(Client client, CancellationToken cancellationToken = default)
    {
        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Client?> GetByIdAsync(Guid clientId, bool tracking, CancellationToken cancellationToken = default)
    {
        IQueryable<Client> query = dbContext.Clients.Include(client => client.Contacts);
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(client => client.Id == clientId, cancellationToken);
    }

    public async Task<IReadOnlyList<Client>> ListAsync(bool includeArchived, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Clients.AsNoTracking().Include(client => client.Contacts).AsQueryable();
        if (!includeArchived) query = query.Where(client => client.ArchivedAt == null);
        return await query.OrderBy(client => client.Name).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InspectionRequest>> GetRequestHistoryAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        await dbContext.Requests.AsNoTracking()
            .Where(request => request.ClientId == clientId)
            .Include(request => request.Items)
            .Include(request => request.Certificates)
            .Include(request => request.History)
            .AsSplitQuery()
            .OrderByDescending(request => request.ReceivedAt)
            .ToListAsync(cancellationToken);

    public async Task<(int Requests, int Items)> CountDependenciesAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        (await dbContext.Requests.IgnoreQueryFilters().CountAsync(request => request.ClientId == clientId, cancellationToken),
         await dbContext.Items.IgnoreQueryFilters().CountAsync(item => item.ClientId == clientId, cancellationToken));

    public async Task RemoveAsync(Client client, CancellationToken cancellationToken = default)
    {
        dbContext.Clients.Remove(client);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}