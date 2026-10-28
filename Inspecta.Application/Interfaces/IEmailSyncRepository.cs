using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

/// <summary>A configured sender plus the name of the client it points to, if any.</summary>
public sealed record SenderRule(ClientMailRule Rule, string? ClientName);

public interface IEmailSyncRepository
{
    Task<IReadOnlyList<CoordinatorEmailConfig>> ListActiveConfigsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SenderRule>> ListSenderRulesAsync(
        Guid managerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a request created from a mailbox message. Returns <c>false</c> without saving when the same
    /// mailbox message (coordinator, UID validity, UID) was already imported, so retries never duplicate it.
    /// </summary>
    Task<bool> SaveRequestIfNewAsync(
        InspectionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Stores how far a mailbox has been read so the next cycle only looks at newer messages.</summary>
    Task SaveSyncCursorAsync(
        Guid coordinatorId,
        string uidValidity,
        long lastUid,
        DateTimeOffset syncFromUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts email requests imported before <paramref name="createdBefore"/> that nobody started working on:
    /// still <c>request_received</c>, no client, location, items or inspector.
    /// </summary>
    Task<int> CountUntouchedEmailRequestsAsync(
        DateTimeOffset createdBefore,
        Guid? managerId,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the requests <see cref="CountUntouchedEmailRequestsAsync"/> counts.</summary>
    Task<int> DeleteUntouchedEmailRequestsAsync(
        DateTimeOffset createdBefore,
        Guid? managerId,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes unconfirmed "maybe" emails imported before <paramref name="createdBefore"/>.</summary>
    Task<int> DeleteExpiredMaybeRequestsAsync(
        DateTimeOffset createdBefore,
        CancellationToken cancellationToken = default);
}
