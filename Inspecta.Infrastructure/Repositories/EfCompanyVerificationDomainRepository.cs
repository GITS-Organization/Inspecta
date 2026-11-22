using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfCompanyVerificationDomainRepository(InspectaDbContext dbContext) : ICompanyVerificationDomainRepository
{
    // The one query that ignores the company filter (registered in FilterBypassGuardTests): see the interface for why. It returns only the id.
    public async Task<Guid?> FindCompanyIdByHostAsync(string host, CancellationToken cancellationToken = default) =>
        await dbContext.CompanyVerificationDomains.IgnoreQueryFilters().AsNoTracking()
            .Where(domain => domain.Host == host)
            .Select(domain => (Guid?)domain.ManagerId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<string?> FindPrimaryHostAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        dbContext.CompanyVerificationDomains.AsNoTracking()
            .Where(domain => domain.ManagerId == companyId && domain.IsPrimary)
            .Select(domain => domain.Host)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CompanyVerificationDomain>> ListAsync(Guid companyId, bool tracking, CancellationToken cancellationToken = default)
    {
        IQueryable<CompanyVerificationDomain> query = dbContext.CompanyVerificationDomains;
        if (!tracking) query = query.AsNoTracking();
        return await query.Where(domain => domain.ManagerId == companyId).ToListAsync(cancellationToken);
    }

    public Task AddAsync(CompanyVerificationDomain domain, CancellationToken cancellationToken = default)
    {
        dbContext.CompanyVerificationDomains.Add(domain);
        return Task.CompletedTask;
    }

    public void Remove(CompanyVerificationDomain domain) => dbContext.CompanyVerificationDomains.Remove(domain);

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
