using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Interfaces;

public interface ICompanyRepository
{
    Task<IReadOnlyList<Company>> ListAsync(
        CancellationToken cancellationToken = default,
        bool includeDeleted = false);
    Task<Company?> GetByIdAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task AddWithManagerAsync(Company company, AppUser manager, CancellationToken cancellationToken = default);
    Task SaveAsync(Company company, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid companyId, CancellationToken cancellationToken = default);
}
