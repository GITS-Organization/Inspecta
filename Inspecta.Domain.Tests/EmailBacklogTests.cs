using Inspecta.Application.Contracts.Mail;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;

namespace Inspecta.Domain.Tests;

public sealed class EmailBacklogTests
{
    [Fact]
    public async Task DryRunReportsTheMatchWithoutDeleting()
    {
        var repository = new FakeSyncRepository(matches: 42);
        var service = new EmailBacklogService(repository);

        var result = await service.CleanupAsync(new CleanupEmailBacklogRequest { CreatedBefore = DateTimeOffset.UtcNow });

        Assert.True(result.DryRun);
        Assert.Equal(42, result.Matched);
        Assert.Equal(0, result.Deleted);
        Assert.False(repository.DeleteCalled);
    }

    [Fact]
    public async Task DeletesTheMatchingRequestsOnlyWhenDryRunIsOff()
    {
        var managerId = Guid.NewGuid();
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-1);
        var repository = new FakeSyncRepository(matches: 42);
        var service = new EmailBacklogService(repository);

        var result = await service.CleanupAsync(new CleanupEmailBacklogRequest
        {
            CreatedBefore = cutoff,
            ManagerId = managerId,
            DryRun = false
        });

        Assert.False(result.DryRun);
        Assert.Equal(42, result.Deleted);
        Assert.Equal((cutoff, (Guid?)managerId), repository.LastCall);
    }

    [Fact]
    public async Task FutureCutoffsAndEmptyManagerIdsAreRejected()
    {
        var service = new EmailBacklogService(new FakeSyncRepository(matches: 1));

        await Assert.ThrowsAsync<ArgumentException>(() => service.CleanupAsync(
            new CleanupEmailBacklogRequest { CreatedBefore = DateTimeOffset.UtcNow.AddDays(1), DryRun = false }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CleanupAsync(
            new CleanupEmailBacklogRequest { CreatedBefore = DateTimeOffset.UtcNow, ManagerId = Guid.Empty, DryRun = false }));
    }

    private sealed class FakeSyncRepository(int matches) : IEmailSyncRepository
    {
        public bool DeleteCalled { get; private set; }
        public (DateTimeOffset, Guid?)? LastCall { get; private set; }

        public Task<int> CountUntouchedEmailRequestsAsync(DateTimeOffset createdBefore, Guid? managerId, CancellationToken cancellationToken = default)
        {
            LastCall = (createdBefore, managerId);
            return Task.FromResult(matches);
        }

        public Task<int> DeleteUntouchedEmailRequestsAsync(DateTimeOffset createdBefore, Guid? managerId, CancellationToken cancellationToken = default)
        {
            DeleteCalled = true;
            LastCall = (createdBefore, managerId);
            return Task.FromResult(matches);
        }

        public Task<IReadOnlyList<CoordinatorEmailConfig>> ListActiveConfigsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CoordinatorEmailConfig>>([]);

        public Task<IReadOnlyList<SenderRule>> ListSenderRulesAsync(Guid managerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SenderRule>>([]);

        public Task<bool> SaveRequestIfNewAsync(InspectionRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task SaveSyncCursorAsync(Guid coordinatorId, string uidValidity, long lastUid, DateTimeOffset syncFromUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<int> DeleteExpiredMaybeRequestsAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
