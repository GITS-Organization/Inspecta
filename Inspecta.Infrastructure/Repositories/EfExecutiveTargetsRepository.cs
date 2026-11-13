using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfExecutiveTargetsRepository(InspectaDbContext dbContext) : IExecutiveTargetsRepository
{
    public async Task<ExecutiveTargets?> GetAsync(Guid managerId, bool tracking = false, CancellationToken cancellationToken = default)
    {
        var query = tracking ? dbContext.ExecutiveTargets : dbContext.ExecutiveTargets.AsNoTracking();
        return await query.SingleOrDefaultAsync(item => item.ManagerId == managerId, cancellationToken);
    }

    public async Task AddAsync(ExecutiveTargets targets, CancellationToken cancellationToken = default)
    {
        dbContext.ExecutiveTargets.Add(targets);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
