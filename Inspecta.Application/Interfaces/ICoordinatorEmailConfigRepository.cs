using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface ICoordinatorEmailConfigRepository
{
    Task<CoordinatorEmailConfig?> GetByCoordinatorAsync(
        Guid coordinatorId,
        CancellationToken cancellationToken = default);

    /// <summary>The row as it is stored now, read straight from the database: never an older copy and never one that is tracked.</summary>
    Task<CoordinatorEmailConfig?> GetByCoordinatorNoTrackingAsync(
        Guid coordinatorId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(CoordinatorEmailConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new refresh token, but only when the row is still a Microsoft sign-in holding <paramref name="expectedEncryptedRefreshToken"/>
    /// (the token that was used). False when the row changed meanwhile (reconnected, switched, removed): nothing is written.
    /// </summary>
    Task<bool> RotateRefreshTokenAsync(
        Guid coordinatorId,
        string expectedEncryptedRefreshToken,
        string newEncryptedRefreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the Microsoft sign-in as needing a reconnect, but only when the row still holds the token Microsoft refused.
    /// False when the row changed meanwhile: a reconnect is never undone, and a password mailbox is never marked.
    /// </summary>
    Task<bool> MarkNeedsReconnectAsync(
        Guid coordinatorId,
        string expectedEncryptedRefreshToken,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remembers the result of a check or test of the saved mailbox: it worked (<paramref name="failure"/> is <c>null</c>) or it failed at <paramref name="step"/> with that reason.
    /// Written only when the mailbox still has the settings it was tried with (<paramref name="expectedUpdatedAt"/>): a result about settings that were changed or reconnected
    /// meanwhile is dropped. Nothing happens when the mailbox was removed meanwhile.
    /// </summary>
    Task RecordCheckAsync(Guid coordinatorId, DateTimeOffset expectedUpdatedAt, DateTimeOffset at, string? failure, string? step, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remembers how a sync of the mailbox went, with the same guards as <see cref="RecordCheckAsync"/>. A sync never ends a sending problem (it does not send), and it only writes
    /// when there is news: the same result is not written again every cycle.
    /// </summary>
    Task RecordSyncAsync(Guid coordinatorId, DateTimeOffset expectedUpdatedAt, DateTimeOffset at, string? failure, string? step, CancellationToken cancellationToken = default);

    /// <summary>Remembers when the newest imported email arrived (only a newer one moves it). Nothing happens when the mailbox was removed meanwhile.</summary>
    Task RecordEmailReceivedAsync(Guid coordinatorId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>Removes the mailbox settings of a coordinator (the mailbox is no longer read or sent from).</summary>
    Task DeleteAsync(CoordinatorEmailConfig config, CancellationToken cancellationToken = default);
}
