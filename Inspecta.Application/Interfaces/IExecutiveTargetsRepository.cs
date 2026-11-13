using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IExecutiveTargetsRepository
{
    /// <summary>The company's targets, or <c>null</c> when it never set them.</summary>
    Task<ExecutiveTargets?> GetAsync(Guid managerId, bool tracking = false, CancellationToken cancellationToken = default);

    Task AddAsync(ExecutiveTargets targets, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
