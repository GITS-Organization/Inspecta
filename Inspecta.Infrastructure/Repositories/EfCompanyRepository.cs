using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfCompanyRepository(InspectaDbContext dbContext) : ICompanyRepository
{
    public Task<Company?> GetByIdAsync(Guid id, bool tracking = false, CancellationToken cancellationToken = default)
    {
        IQueryable<Company> query = dbContext.Companies;
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(company => company.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, Company>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return new Dictionary<Guid, Company>();
        var wanted = ids.ToArray();
        return await dbContext.Companies.AsNoTracking().Where(company => wanted.Contains(company.Id))
            .ToDictionaryAsync(company => company.Id, cancellationToken);
    }

    // Ordered after loading: SQLite, used by the tests, cannot order by a date with an offset.
    public async Task<IReadOnlyList<Company>> ListAsync(CancellationToken cancellationToken = default) =>
        (await dbContext.Companies.AsNoTracking().ToListAsync(cancellationToken))
        .OrderBy(company => company.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public async Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        dbContext.Companies.Add(company);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
