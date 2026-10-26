using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface ICompanyRepository
{
    Task<IReadOnlyList<Company>> ListAsync(CancellationToken cancellationToken = default);
    Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task AddWithManagerAsync(Company company, AppUser manager, CancellationToken cancellationToken = default);
}
