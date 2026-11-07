using Inspecta.Application.Contracts.Email;

namespace Inspecta.Application.Interfaces;

public interface ICoordinatorEmailConfigService
{
    Task UpdateAsync(
        Guid managerId,
        Guid coordinatorId,
        CoordinatorEmailConfigRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>The coordinator's mailbox settings without the password; not configured yet gives the default import date.</summary>
    Task<CoordinatorEmailConfigResponse> GetAsync(Guid managerId, Guid coordinatorId, CancellationToken cancellationToken = default);

    /// <summary>A coordinator's own mailbox settings and connection status (never the password or a token).</summary>
    Task<CoordinatorEmailConfigResponse> GetOwnAsync(Guid coordinatorId, CancellationToken cancellationToken = default);

    /// <summary>Removes the coordinator's mailbox settings (a Microsoft sign-in or a password): the mailbox is no longer read or sent from.</summary>
    Task DeleteAsync(Guid managerId, Guid coordinatorId, CancellationToken cancellationToken = default);

    /// <summary>Starts importing mail again from a chosen date, without touching the mailbox settings.</summary>
    Task<CoordinatorEmailConfigResponse> ChangeSyncFromAsync(
        Guid managerId, Guid coordinatorId, DateTimeOffset syncFrom, CancellationToken cancellationToken = default);
}
