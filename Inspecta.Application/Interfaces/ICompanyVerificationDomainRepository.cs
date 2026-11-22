using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

/// <summary>The verification addresses of the companies. Everything but <see cref="FindCompanyIdByHostAsync"/> is scoped to one company.</summary>
public interface ICompanyVerificationDomainRepository
{
    /// <summary>
    /// The company that owns a host, or null when no company does. Ignores the company filter on purpose: the public page has no
    /// signed-in user, and a company must be told when a host it asks for already belongs to another. Returns only the id.
    /// </summary>
    Task<Guid?> FindCompanyIdByHostAsync(string host, CancellationToken cancellationToken = default);

    /// <summary>The host new certificates of the company use, or null when it has no primary address. If two primaries ever exist, the newest wins (then the lower host).</summary>
    Task<string?> FindPrimaryHostAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The company's addresses (tracked when it is going to change them), in no particular order.</summary>
    Task<IReadOnlyList<CompanyVerificationDomain>> ListAsync(Guid companyId, bool tracking, CancellationToken cancellationToken = default);

    Task AddAsync(CompanyVerificationDomain domain, CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}
