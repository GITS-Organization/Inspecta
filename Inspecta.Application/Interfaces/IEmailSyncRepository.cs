using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IEmailSyncRepository
{
    Task<IReadOnlyList<CoordinatorEmailConfig>> ListActiveConfigsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a request created from a mailbox message. Returns <c>false</c> without saving when the same
    /// mailbox message (coordinator, UID validity, UID) was already imported, so retries never duplicate it.
    /// </summary>
    Task<bool> SaveRequestIfNewAsync(
        InspectionRequest request,
        CancellationToken cancellationToken = default);
}
