using System.Collections.Concurrent;
using Inspecta.Application.Exceptions;

namespace Inspecta.Application.Services;

/// <summary>
/// Trying a mailbox connects to a server somebody typed, so it is limited: ten tries a minute for a company, whichever way the mailbox is tried
/// (a test, a check, or saving or creating with <c>verify</c>). Saving without trying is never limited.
/// </summary>
public sealed class MailboxTryLimiter(TimeProvider? clock = null)
{
    public const int Tries = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private readonly ConcurrentDictionary<Guid, Queue<DateTimeOffset>> recent = new();

    /// <exception cref="AuthException"><c>too_many_tries</c> (429) when the company already tried ten times in the last minute.</exception>
    public void Take(Guid companyId)
    {
        var now = time.GetUtcNow();
        var queue = recent.GetOrAdd(companyId, _ => new Queue<DateTimeOffset>());
        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() >= Window) queue.Dequeue();
            if (queue.Count >= Tries)
                throw new AuthException("too_many_tries", "Too many tries. Wait a minute and try again.", 429);
            queue.Enqueue(now);
        }
    }
}
