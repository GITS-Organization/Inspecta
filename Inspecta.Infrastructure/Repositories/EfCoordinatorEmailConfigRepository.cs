using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfCoordinatorEmailConfigRepository(InspectaDbContext dbContext)
    : ICoordinatorEmailConfigRepository
{
    public Task<CoordinatorEmailConfig?> GetByCoordinatorAsync(
        Guid coordinatorId,
        CancellationToken cancellationToken = default) =>
        dbContext.CoordinatorEmailConfigs
            .SingleOrDefaultAsync(config => config.CoordinatorId == coordinatorId, cancellationToken);

    public async Task SaveAsync(
        CoordinatorEmailConfig config,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Entry(config).State == EntityState.Detached)
            dbContext.CoordinatorEmailConfigs.Add(config);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
