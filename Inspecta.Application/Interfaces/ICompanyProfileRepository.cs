using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface ICompanyProfileRepository
{
    Task<CompanyProfile?> GetByManagerAsync(Guid managerId, bool tracking, CancellationToken cancellationToken = default);
    Task AddAsync(CompanyProfile profile, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
