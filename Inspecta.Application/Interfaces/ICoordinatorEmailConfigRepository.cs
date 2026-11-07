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

    /// <summary>Removes the mailbox settings of a coordinator (the mailbox is no longer read or sent from).</summary>
    Task DeleteAsync(CoordinatorEmailConfig config, CancellationToken cancellationToken = default);
}
