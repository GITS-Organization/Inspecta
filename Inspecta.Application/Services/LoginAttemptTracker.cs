using System.Collections.Concurrent;

namespace Inspecta.Application.Services;

/// <summary>Counts failed sign-ins per email so that guessing a password is slow.</summary>
public interface ILoginAttemptTracker
{
    /// <summary>How long the email is still locked, or null when it may try.</summary>
    TimeSpan? LockedFor(string email);

    void RecordFailure(string email);

    void RecordSuccess(string email);
}

/// <summary>
/// Five failed sign-ins within fifteen minutes lock the email for fifteen minutes, whether or not the email belongs to a user (so the lock
/// reveals nothing). The counters live in memory: the service runs as one instance, and a restart only forgets them.
/// </summary>
public sealed class LoginAttemptTracker(TimeProvider? clock = null) : ILoginAttemptTracker
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);
    private const int MaxTrackedEmails = 50_000;

    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, State> states = new();

    public TimeSpan? LockedFor(string email)
    {
        if (!states.TryGetValue(Key(email), out var state)) return null;
        lock (state)
        {
            var remaining = state.LockedUntil - clock.GetUtcNow();
            return remaining > TimeSpan.Zero ? remaining : null;
        }
    }

    public void RecordFailure(string email)
    {
        var now = clock.GetUtcNow();
        if (states.Count >= MaxTrackedEmails) Purge(now);
        var state = states.GetOrAdd(Key(email), _ => new State());
        lock (state)
        {
            if (now - state.WindowStart > Window)
            {
                state.WindowStart = now;
                state.Failures = 0;
            }

            state.Failures++;
            if (state.Failures >= MaxFailures)
            {
                state.LockedUntil = now + Lockout;
                state.Failures = 0;
                state.WindowStart = now;
            }
        }
    }

    public void RecordSuccess(string email) => states.TryRemove(Key(email), out _);

    private static string Key(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    // Keeps memory bounded when somebody tries many different emails: old, unlocked entries go first.
    private void Purge(DateTimeOffset now)
    {
        foreach (var (key, state) in states)
        {
            lock (state)
            {
                if (state.LockedUntil <= now && now - state.WindowStart > Window) states.TryRemove(key, out _);
            }
        }
    }

    private sealed class State
    {
        public int Failures;
        public DateTimeOffset WindowStart = DateTimeOffset.MinValue;
        public DateTimeOffset LockedUntil = DateTimeOffset.MinValue;
    }
}
