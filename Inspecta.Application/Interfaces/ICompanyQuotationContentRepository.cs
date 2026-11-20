using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface ICompanyQuotationContentRepository
{
    /// <summary>The content the company saved, or <c>null</c> when it never did. Read by company id only: the table has no tenant filter.</summary>
    Task<CompanyQuotationContent?> GetAsync(Guid companyId, bool tracking, CancellationToken cancellationToken = default);

    Task AddAsync(CompanyQuotationContent content, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
