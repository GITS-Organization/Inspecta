using Inspecta.Api.BackgroundServices;

namespace Inspecta.Domain.Tests;

public sealed class KeyedGateTests
{
    private readonly object counters = new();

    private async Task Track(KeyedGate<Guid> gate, Guid key, Action<int> seen, Func<int> enter, Action leave)
    {
        await gate.RunAsync(key, async () =>
        {
            seen(enter());
            await Task.Delay(30);
            leave();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task TwoRunsForTheSameKeyNeverOverlap()
    {
        var gate = new KeyedGate<Guid>();
        var team = Guid.NewGuid();
        var running = 0;
        var most = 0;

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Track(
            gate, team,
            seen => { lock (counters) most = Math.Max(most, seen); },
            () => Interlocked.Increment(ref running),
            () => Interlocked.Decrement(ref running))));

        Assert.Equal(1, most);
    }

    [Fact]
    public async Task RunsForDifferentKeysDoOverlap()
    {
        var gate = new KeyedGate<Guid>();
        var running = 0;
        var most = 0;

        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Track(
            gate, Guid.NewGuid(),
            seen => { lock (counters) most = Math.Max(most, seen); },
            () => Interlocked.Increment(ref running),
            () => Interlocked.Decrement(ref running))));

        Assert.True(most >= 2);
    }

    [Fact]
    public async Task AFailingRunReleasesTheKeyForTheNext()
    {
        var gate = new KeyedGate<Guid>();
        var team = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gate.RunAsync(team, () => throw new InvalidOperationException("boom"), CancellationToken.None));

        var ran = false;
        await gate.RunAsync(team, () => { ran = true; return Task.CompletedTask; }, CancellationToken.None);
        Assert.True(ran);
    }
}
