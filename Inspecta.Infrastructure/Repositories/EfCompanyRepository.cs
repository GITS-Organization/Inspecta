using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfCompanyRepository(InspectaDbContext dbContext) : ICompanyRepository
{
    public async Task<IReadOnlyList<Company>> ListAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Companies.AsNoTracking().OrderBy(company => company.Name).ToListAsync(cancellationToken);

    public Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        dbContext.Companies.AsNoTracking().SingleOrDefaultAsync(company => company.Id == companyId, cancellationToken);

    public Task<bool> ExistsAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        dbContext.Companies.AnyAsync(company => company.Id == companyId && company.IsActive, cancellationToken);

    public async Task AddWithManagerAsync(
        Company company,
        AppUser manager,
        CancellationToken cancellationToken = default)
    {
        dbContext.Companies.Add(company);
        dbContext.Users.Add(manager);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
