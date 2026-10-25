using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfEquipmentRepository(InspectaDbContext dbContext) : IEquipmentRepository
{
    public async Task AddAsync(Item equipment, CancellationToken cancellationToken = default)
    {
        dbContext.Items.Add(equipment);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Item?> GetByIdAsync(Guid equipmentId, bool tracking, CancellationToken cancellationToken = default)
    {
        IQueryable<Item> query = dbContext.Items;
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(equipment => equipment.Id == equipmentId, cancellationToken);
    }

    public async Task<IReadOnlyList<Item>> ListAsync(Guid? clientId, bool includeArchived, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Items.AsNoTracking().AsQueryable();
        if (clientId is Guid id) query = query.Where(equipment => equipment.ClientId == id);
        if (!includeArchived) query = query.Where(equipment => equipment.ArchivedAt == null);
        return await query.OrderBy(equipment => equipment.Name).ToListAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);

    public Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        dbContext.Clients.AnyAsync(client => client.Id == clientId && client.ArchivedAt == null, cancellationToken);
}