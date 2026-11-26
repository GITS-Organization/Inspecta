using Inspecta.Api.BackgroundServices;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

public sealed class GatedRequestImportTests
{
    private sealed class SlowRepository : IEmailSyncRepository
    {
        private int running;
        public int MostAtOnce { get; private set; }

        public async Task<bool> SaveRequestIfNewAsync(InspectionRequest request, CancellationToken cancellationToken = default)
        {
            var now = Interlocked.Increment(ref running);
            lock (this) MostAtOnce = Math.Max(MostAtOnce, now);
            await Task.Delay(60, cancellationToken);
            Interlocked.Decrement(ref running);
            return true;
        }

        public Task<Guid?> FindSameEmailOwnerAsync(InspectionRequest request, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);
        public Task<IReadOnlyList<CoordinatorEmailConfig>> ListActiveConfigsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SenderRule>> ListSenderRulesAsync(Guid managerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> PromoteMatchingMaybesAsync(Guid managerId, IReadOnlyList<SenderRule> senders, Func<InspectionRequest, Task<IReadOnlyList<RequestItem>>>? autofill = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveSyncCursorAsync(Guid coordinatorId, string uidValidity, long lastUid, DateTimeOffset syncFromUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountUntouchedEmailRequestsAsync(DateTimeOffset createdBefore, Guid? managerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> DeleteUntouchedEmailRequestsAsync(DateTimeOffset createdBefore, Guid? managerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> DeleteExpiredMaybeRequestsAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static InspectionRequest Request(Guid manager, long uid) => InspectionRequest.CreateFromEmail(
        manager, Guid.NewGuid(),
        new IncomingEmail("1", uid, "text", null, "a@b.com", "A", "S", [], [], [], [], [], "<x@y>", null, null, null),
        triage: RequestTriage.Maybe);

    [Fact]
    public async Task ImportsOfOneCompanyRunOneAtATimeAndOtherCompaniesSideBySide()
    {
        var company = Guid.NewGuid();
        var same = new SlowRepository();
        await Task.WhenAll(Enumerable.Range(1, 4).Select(uid => GatedRequestImport.SaveAsync(same, Request(company, uid), CancellationToken.None)));
        Assert.Equal(1, same.MostAtOnce);

        var different = new SlowRepository();
        await Task.WhenAll(Enumerable.Range(1, 4).Select(uid => GatedRequestImport.SaveAsync(different, Request(Guid.NewGuid(), uid), CancellationToken.None)));
        Assert.True(different.MostAtOnce > 1);
    }
}
