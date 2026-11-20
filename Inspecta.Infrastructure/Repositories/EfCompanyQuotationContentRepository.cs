using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfCompanyQuotationContentRepository(InspectaDbContext dbContext) : ICompanyQuotationContentRepository
{
    public Task<CompanyQuotationContent?> GetAsync(Guid companyId, bool tracking, CancellationToken cancellationToken = default)
    {
        IQueryable<CompanyQuotationContent> query = dbContext.CompanyQuotationContents;
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(content => content.CompanyId == companyId, cancellationToken);
    }

    public async Task AddAsync(CompanyQuotationContent content, CancellationToken cancellationToken = default)
    {
        dbContext.CompanyQuotationContents.Add(content);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
