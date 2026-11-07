using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface ICoordinatorEmailConfigRepository
{
    Task<CoordinatorEmailConfig?> GetByCoordinatorAsync(
        Guid coordinatorId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(CoordinatorEmailConfig config, CancellationToken cancellationToken = default);

    /// <summary>Removes the mailbox settings of a coordinator (the mailbox is no longer read or sent from).</summary>
    Task DeleteAsync(CoordinatorEmailConfig config, CancellationToken cancellationToken = default);
}
