using Inspecta.Api.BackgroundServices;

namespace Inspecta.Domain.Tests;

public sealed class BoundedParallelTests
{
    private readonly object gate = new();

    [Fact]
    public async Task NeverRunsMoreThanTheLimitAndFinishesAll()
    {
        var running = 0;
        var most = 0;
        var done = 0;

        await BoundedParallel.ForEachAsync(Enumerable.Range(0, 20), 3, async (_, cancellationToken) =>
        {
            var now = Interlocked.Increment(ref running);
            lock (gate) most = Math.Max(most, now);
            await Task.Delay(20, cancellationToken);
            Interlocked.Decrement(ref running);
            Interlocked.Increment(ref done);
        }, CancellationToken.None);

        Assert.Equal(20, done);
        Assert.InRange(most, 2, 3);
    }

    [Fact]
    public async Task ALimitBelowOneRunsOneAtATime()
    {
        var running = 0;
        var most = 0;

        await BoundedParallel.ForEachAsync(Enumerable.Range(0, 5), 0, async (_, cancellationToken) =>
        {
            var now = Interlocked.Increment(ref running);
            lock (gate) most = Math.Max(most, now);
            await Task.Delay(10, cancellationToken);
            Interlocked.Decrement(ref running);
        }, CancellationToken.None);

        Assert.Equal(1, most);
    }
}
