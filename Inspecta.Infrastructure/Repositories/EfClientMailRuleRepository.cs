using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfClientMailRuleRepository(InspectaDbContext dbContext) : IClientMailRuleRepository
{
    public async Task<IReadOnlyList<ClientMailRule>> ListAsync(Guid? managerId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.ClientMailRules.AsNoTracking().AsQueryable();
        if (managerId is Guid id) query = query.Where(rule => rule.ManagerId == id);
        return await query.ToListAsync(cancellationToken);
    }

    public Task<ClientMailRule?> GetByIdAsync(Guid ruleId, CancellationToken cancellationToken = default) =>
        dbContext.ClientMailRules.SingleOrDefaultAsync(rule => rule.Id == ruleId, cancellationToken);

    public Task<bool> ExistsAsync(Guid managerId, string pattern, CancellationToken cancellationToken = default) =>
        dbContext.ClientMailRules.AnyAsync(rule => rule.ManagerId == managerId && rule.Pattern == pattern, cancellationToken);

    public Task<Guid?> GetClientManagerIdAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        dbContext.Clients
            .Where(client => client.Id == clientId && client.ArchivedAt == null)
            .Select(client => client.ManagerId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task AddAsync(ClientMailRule rule, CancellationToken cancellationToken = default)
    {
        dbContext.ClientMailRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ClientMailRule rule, CancellationToken cancellationToken = default)
    {
        dbContext.ClientMailRules.Remove(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
