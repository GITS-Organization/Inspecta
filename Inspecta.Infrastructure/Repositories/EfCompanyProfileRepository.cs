using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfCompanyProfileRepository(InspectaDbContext dbContext) : ICompanyProfileRepository
{
    public Task<CompanyProfile?> GetByManagerAsync(Guid managerId, bool tracking, CancellationToken cancellationToken = default)
    {
        IQueryable<CompanyProfile> query = dbContext.CompanyProfiles;
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(profile => profile.ManagerId == managerId, cancellationToken);
    }

    public async Task AddAsync(CompanyProfile profile, CancellationToken cancellationToken = default)
    {
        dbContext.CompanyProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
