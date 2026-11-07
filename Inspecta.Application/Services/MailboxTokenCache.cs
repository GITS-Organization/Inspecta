using System.Collections.Concurrent;

namespace Inspecta.Application.Services;

/// <summary>Access tokens of Microsoft mailboxes, kept in memory only until shortly before they expire.</summary>
public sealed class MailboxTokenCache
{
    /// <summary>A token with less time left than this is not reused.</summary>
    public static readonly TimeSpan Margin = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<Guid, (string Token, DateTimeOffset ExpiresAt)> tokens = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> gates = new();

    public bool TryGet(Guid coordinatorId, DateTimeOffset now, out string token)
    {
        if (tokens.TryGetValue(coordinatorId, out var entry) && entry.ExpiresAt - now > Margin)
        {
            token = entry.Token;
            return true;
        }

        token = string.Empty;
        return false;
    }

    public void Set(Guid coordinatorId, string token, DateTimeOffset expiresAt) => tokens[coordinatorId] = (token, expiresAt);

    public void Remove(Guid coordinatorId) => tokens.TryRemove(coordinatorId, out _);

    /// <summary>One renewal at a time per mailbox: the sync and a send must not both spend the same refresh token.</summary>
    public SemaphoreSlim GateFor(Guid coordinatorId) => gates.GetOrAdd(coordinatorId, _ => new SemaphoreSlim(1, 1));
}
