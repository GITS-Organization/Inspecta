using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface ICoordinatorEmailConfigRepository
{
    Task<CoordinatorEmailConfig?> GetByCoordinatorAsync(
        Guid coordinatorId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(CoordinatorEmailConfig config, CancellationToken cancellationToken = default);
}
