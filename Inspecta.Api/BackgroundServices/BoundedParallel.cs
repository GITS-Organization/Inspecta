namespace Inspecta.Api.BackgroundServices;

/// <summary>Runs work for many items at once, but never more than a limit at a time.</summary>
public static class BoundedParallel
{
    /// <summary>A limit below 1 runs one item at a time.</summary>
    public static Task ForEachAsync<T>(
        IEnumerable<T> items,
        int maxParallel,
        Func<T, CancellationToken, Task> work,
        CancellationToken cancellationToken) =>
        Parallel.ForEachAsync(
            items,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, maxParallel), CancellationToken = cancellationToken },
            async (item, token) => await work(item, token));
}
