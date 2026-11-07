using System.Collections.Concurrent;

namespace Inspecta.Api.BackgroundServices;

/// <summary>Runs work one at a time per key (work for different keys still runs side by side).</summary>
public sealed class KeyedGate<TKey> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, SemaphoreSlim> gates = new();

    public async Task RunAsync(TKey key, Func<Task> work, CancellationToken cancellationToken)
    {
        var gate = gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await work();
        }
        finally
        {
            gate.Release();
        }
    }
}
