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

    /// <summary>Starts importing mail again from a chosen date, without touching the mailbox settings.</summary>
    Task<CoordinatorEmailConfigResponse> ChangeSyncFromAsync(
        Guid managerId, Guid coordinatorId, DateTimeOffset syncFrom, CancellationToken cancellationToken = default);
}
