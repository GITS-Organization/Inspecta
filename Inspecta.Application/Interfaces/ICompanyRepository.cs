using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, bool tracking = false, CancellationToken cancellationToken = default);

    /// <summary>The given companies in one query (ids that do not exist are left out).</summary>
    Task<IReadOnlyDictionary<Guid, Company>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Company>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Company company, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
