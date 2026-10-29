using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfEquipmentCatalog(InspectaDbContext dbContext) : IEquipmentCatalog
{
    public Task<Client?> FindClientByNameAsync(Guid managerId, string name, CancellationToken cancellationToken = default)
    {
        var lowered = name.Trim().ToLower();
        return dbContext.Clients.FirstOrDefaultAsync(
            client => client.ManagerId == managerId && client.ArchivedAt == null && client.Name.ToLower() == lowered,
            cancellationToken);
    }

    public async Task<Item?> FindEquipmentAsync(
        Guid clientId,
        string name,
        string? serialNumber,
        string? capacity,
        CancellationToken cancellationToken = default)
    {
        var lowered = name.Trim().ToLower();
        var candidates = await dbContext.Items
            .Where(item => item.ClientId == clientId && item.ArchivedAt == null && item.Name.ToLower() == lowered)
            .ToListAsync(cancellationToken);
        return candidates.FirstOrDefault(item =>
            Same(item.SerialNumber, serialNumber) && Same(item.Capacity, capacity));
    }

    public void Stage(Client client) => dbContext.Clients.Add(client);

    public void Stage(Item equipment) => dbContext.Items.Add(equipment);

    private static bool Same(string? left, string? right) =>
        string.Equals(
            string.IsNullOrWhiteSpace(left) ? null : left.Trim(),
            string.IsNullOrWhiteSpace(right) ? null : right.Trim(),
            StringComparison.OrdinalIgnoreCase);
}
