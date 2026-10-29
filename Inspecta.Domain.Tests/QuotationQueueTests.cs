using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>An accepted quotation waits unassigned, goes to an inspector, whose only change is the date, then to certification.</summary>
public sealed class QuotationQueueTests
{
    private static readonly Guid ManagerId = QuotationTests.ManagerId;
    private static readonly Guid CoordinatorId = QuotationTests.CoordinatorId;

    [Fact]
    public async Task AcceptingAQuotationLeavesItUnassignedInTheUnassignedQueue()
    {
        var fixture = new Fixture();

        await fixture.AcceptAsync();

        Assert.Equal(JobStatus.Unassigned, fixture.Request.Status);
        var row = Assert.Single(await fixture.QueueAsync(CoordinatorId, "coordinator", QuotationQueue.Unassigned));
        Assert.Equal(QuotationQueue.Unassigned, row.Queue);
        Assert.Equal(QuoteStage.Accepted, row.Stage);
        Assert.Equal(JobStatus.Unassigned, row.RequestStatus);
        Assert.NotNull(row.AmountPiastres);
        Assert.Empty(await fixture.QueueAsync(CoordinatorId, "coordinator", QuotationQueue.Assigned));
        Assert.Empty(await fixture.QueueAsync(CoordinatorId, "coordinator", QuotationQueue.Certify));
    }

    [Fact]
    public async Task AQuotationThatIsNotAcceptedIsInNoQueue()
    {
        var fixture = new Fixture();
        await fixture.Quotes.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());

        foreach (var queue in Enum.GetValues<QuotationQueue>())
            Assert.Empty(await fixture.QueueAsync(CoordinatorId, "coordinator", queue));
        Assert.Null(Assert.Single(await fixture.Quotes.Service.ListVisibleAsync(CoordinatorId, "coordinator", new QuoteListQuery())).Queue);
    }

    [Fact]
    public async Task AssigningMovesTheQuotationToTheAssignedQueueWithItsInspector()
    {
        var fixture = new Fixture();
        await fixture.AcceptAsync();

        await fixture.Requests.AssignAsync(fixture.Request.Id, CoordinatorId, "coordinator", fixture.Karim.Id, fixture.Date(2), null);

        Assert.Empty(await fixture.QueueAsync(CoordinatorId, "coordinator", QuotationQueue.Unassigned));
        var row = Assert.Single(await fixture.QueueAsync(CoordinatorId, "coordinator", QuotationQueue.Assigned));
        Assert.Equal(QuotationQueue.Assigned, row.Queue);
        Assert.Equal("Eng. Karim", row.InspectorName);
        Assert.Equal(fixture.Date(2), row.ScheduledAt);
        Assert.Single(await fixture.QueueAsync(
            CoordinatorId, "coordinator", QuotationQueue.Assigned, new QuotationQueueQuery(InspectorId: fixture.Karim.Id)));
        Assert.Empty(await fixture.QueueAsync(
            CoordinatorId, "coordinator", QuotationQueue.Assigned, new QuotationQueueQuery(InspectorId: Guid.NewGuid())));
    }

    [Fact]
    public async Task TheAssignedQueueStaysUntilTheCertificateIsSubmitted()
    {
        var fixture = new Fixture();
        await fixture.AcceptAsync();
        await fixture.Requests.AssignAsync(fixture.Request.Id, CoordinatorId, "coordinator", fixture.Karim.Id, fixture.Date(2), null);

        fixture.Request.TransitionTo(JobStatus.TaskAccepted, fixture.Karim.Id);
        fixture.Request.TransitionTo(JobStatus.InProgress, fixture.Karim.Id);
        Assert.Single(await fixture.QueueAsync(CoordinatorId, "coordinator", QuotationQueue.Assigned));
        Assert.Empty(await fixture.QueueAsync(CoordinatorId, "coordinator", QuotationQueue.Certify));

        fixture.Request.TransitionTo(JobStatus.CertificateSubmitted, fixture.Karim.Id);
        Assert.Empty(await fixture.QueueAsync(CoordinatorId, "coordinator", QuotationQueue.Assigned));
        var row = Assert.Single(await fixture.QueueAsync(CoordinatorId, "coordinator", QuotationQueue.Certify));
        Assert.Equal(QuotationQueue.Certify, row.Queue);
    }

    [Fact]
    public async Task AnInspectorSeesOnlyTheirOwnJobsWithoutPricesAndNothingUnassigned()
    {
        var fixture = new Fixture();
        await fixture.AcceptAsync();

        Assert.Empty(await fixture.QueueAsync(fixture.Karim.Id, "inspector", QuotationQueue.Unassigned));

        await fixture.Requests.AssignAsync(fixture.Request.Id, CoordinatorId, "coordinator", fixture.Karim.Id, fixture.Date(2), null);

        var row = Assert.Single(await fixture.QueueAsync(fixture.Karim.Id, "inspector", QuotationQueue.Assigned));
        Assert.Equal("Industrial City", row.Location);
        Assert.Null(row.AmountPiastres);
        Assert.False(row.CanEdit);
        Assert.Empty(await fixture.QueueAsync(fixture.Sara.Id, "inspector", QuotationQueue.Assigned));
        // Naming someone else does not widen what an inspector gets.
        Assert.Empty(await fixture.QueueAsync(
            fixture.Sara.Id, "inspector", QuotationQueue.Assigned, new QuotationQueueQuery(InspectorId: fixture.Karim.Id)));
    }

    [Fact]
    public async Task AnotherCoordinatorDoesNotSeeTheQueueOfAnEmailRequestTheyDidNotReceive()
    {
        var fixture = new Fixture();
        await fixture.AcceptAsync();

        Assert.Empty(await fixture.QueueAsync(Guid.NewGuid(), "coordinator", QuotationQueue.Unassigned));
        Assert.Single(await fixture.QueueAsync(ManagerId, "technical_manager", QuotationQueue.Unassigned));
    }

    [Fact]
    public async Task TheJobCanBeHandedToAnotherInspectorUntilTheWorkStarts()
    {
        var fixture = new Fixture();
        await fixture.AcceptAsync();
        await fixture.Requests.AssignAsync(fixture.Request.Id, CoordinatorId, "coordinator", fixture.Karim.Id, fixture.Date(2), null);

        await fixture.Requests.AssignAsync(fixture.Request.Id, CoordinatorId, "coordinator", fixture.Sara.Id, fixture.Date(3), "Karim is away");

        Assert.Equal(fixture.Sara.Id, fixture.Request.InspectorId);
        Assert.Equal(JobStatus.Assigned, fixture.Request.Status);

        fixture.Request.TransitionTo(JobStatus.TaskAccepted, fixture.Sara.Id);
        await fixture.Requests.AssignAsync(fixture.Request.Id, CoordinatorId, "coordinator", fixture.Karim.Id, fixture.Date(4), null);
        Assert.Equal(fixture.Karim.Id, fixture.Request.InspectorId);
        Assert.Equal(JobStatus.Assigned, fixture.Request.Status);

        fixture.Request.TransitionTo(JobStatus.TaskAccepted, fixture.Karim.Id);
        fixture.Request.TransitionTo(JobStatus.InProgress, fixture.Karim.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Requests.AssignAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", fixture.Sara.Id, fixture.Date(5), null));
    }

    [Fact]
    public async Task TheInspectorCanMoveTheDateOfTheirOwnJob()
    {
        var fixture = new Fixture();
        await fixture.AcceptAsync();
        await fixture.Requests.AssignAsync(fixture.Request.Id, CoordinatorId, "coordinator", fixture.Karim.Id, fixture.Date(2), null);

        var moved = await fixture.Requests.ChangeInspectionDateAsync(
            fixture.Request.Id, fixture.Karim.Id, "inspector", fixture.Date(5), "Site closed that day");

        Assert.Equal(fixture.Date(5), moved.ScheduledAt);
        Assert.Equal(fixture.Karim.Id, moved.InspectorId);
        Assert.Equal(JobStatus.Assigned, moved.Status);
        var entry = fixture.Request.History.Last();
        Assert.Equal(fixture.Karim.Id, entry.ActorId);
        Assert.Contains("Site closed that day", entry.Note);
        Assert.Equal(fixture.Date(5), Assert.Single(await fixture.QueueAsync(
            fixture.Karim.Id, "inspector", QuotationQueue.Assigned)).ScheduledAt);
    }

    [Fact]
    public async Task AnotherInspectorCannotMoveTheDateAndTheJobIsNotTheirs()
    {
        var fixture = new Fixture();
        await fixture.AcceptAsync();
        await fixture.Requests.AssignAsync(fixture.Request.Id, CoordinatorId, "coordinator", fixture.Karim.Id, fixture.Date(2), null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Requests.ChangeInspectionDateAsync(
            fixture.Request.Id, fixture.Sara.Id, "inspector", fixture.Date(5), null));
        Assert.Equal(fixture.Date(2), fixture.Request.ScheduledAt);
    }

    [Fact]
    public async Task TheDateCannotMoveBeforeAssignmentOrAfterTheWorkStarted()
    {
        var fixture = new Fixture();
        await fixture.AcceptAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Requests.ChangeInspectionDateAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", fixture.Date(5), null));

        await fixture.Requests.AssignAsync(fixture.Request.Id, CoordinatorId, "coordinator", fixture.Karim.Id, fixture.Date(2), null);
        fixture.Request.TransitionTo(JobStatus.TaskAccepted, fixture.Karim.Id);
        await fixture.Requests.ChangeInspectionDateAsync(fixture.Request.Id, fixture.Karim.Id, "inspector", fixture.Date(3), null);

        fixture.Request.TransitionTo(JobStatus.InProgress, fixture.Karim.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Requests.ChangeInspectionDateAsync(
            fixture.Request.Id, fixture.Karim.Id, "inspector", fixture.Date(5), null));
        Assert.Equal(fixture.Date(3), fixture.Request.ScheduledAt);
    }

    [Fact]
    public async Task TheCoordinatorAndManagersCanMoveTheDateButAnotherCoordinatorCannot()
    {
        var fixture = new Fixture();
        await fixture.AcceptAsync();
        await fixture.Requests.AssignAsync(fixture.Request.Id, CoordinatorId, "coordinator", fixture.Karim.Id, fixture.Date(2), null);

        await fixture.Requests.ChangeInspectionDateAsync(fixture.Request.Id, CoordinatorId, "coordinator", fixture.Date(3), null);
        await fixture.Requests.ChangeInspectionDateAsync(fixture.Request.Id, ManagerId, "technical_manager", fixture.Date(4), null);
        await fixture.Requests.ChangeInspectionDateAsync(fixture.Request.Id, Guid.NewGuid(), "admin", fixture.Date(5), null);
        Assert.Equal(fixture.Date(5), fixture.Request.ScheduledAt);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Requests.ChangeInspectionDateAsync(
            fixture.Request.Id, Guid.NewGuid(), "coordinator", fixture.Date(6), null));
    }

    [Fact]
    public async Task TheCoordinatorCannotPickADateInsideTheInspectorsLeave()
    {
        var fixture = new Fixture();
        await fixture.AcceptAsync();
        await fixture.Requests.AssignAsync(fixture.Request.Id, CoordinatorId, "coordinator", fixture.Karim.Id, fixture.Date(2), null);
        fixture.Karim.SetLeave(fixture.Date(10));

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Requests.ChangeInspectionDateAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", fixture.Date(5), null));
    }

    [Fact]
    public void EveryRequestStatusAfterAcceptanceBelongsToExactlyOneQueue()
    {
        var afterAcceptance = Enum.GetValues<JobStatus>().Where(status => status >= JobStatus.Unassigned);

        foreach (var status in afterAcceptance)
            Assert.Equal(1, Enum.GetValues<QuotationQueue>().Count(queue => QuotationQueues.Statuses(queue).Contains(status)));
        foreach (var status in Enum.GetValues<JobStatus>().Where(status => status < JobStatus.Unassigned))
            Assert.Null(QuotationQueues.Of(status));
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Karim = AppUser.Create("karim@example.com", "Eng. Karim", UserRole.Inspector, managerId: ManagerId);
            Sara = AppUser.Create("sara@example.com", "Eng. Sara", UserRole.Inspector, managerId: ManagerId);
            var inner = new QuotationTests.Fixture(extraUsers: [Karim, Sara]);
            Request = inner.Request;
            Crane = inner.Crane;
            Receiver = inner.Receiver;
            Quotes = (inner.Service, inner.Quotes);
            Quotes.Repository.RequestOf = id => id == Request.Id ? Request : null;
            Requests = new InspectionRequestService(
                inner.Requests,
                new IncomingEmailRequestTests.FakeUserRepository([Karim, Sara]));
        }

        public AppUser Karim { get; }
        public AppUser Sara { get; }
        public InspectionRequest Request { get; }
        public RequestItem Crane { get; }
        public RequestItem Receiver { get; }
        public (QuoteService Service, QuotationTests.FakeQuotes Repository) Quotes { get; }
        public InspectionRequestService Requests { get; }

        public DateTimeOffset Date(int days) => new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero).AddDays(days);

        public async Task AcceptAsync()
        {
            var quote = await Quotes.Service.CreateAsync(Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
            await Quotes.Service.UpdateAsync(Request.Id, quote.Id, CoordinatorId, "coordinator", new UpdateQuoteRequest
            {
                Items =
                [
                    new UpdateQuoteItem { RequestItemId = Crane.Id, UnitPricePiastres = 1_200_000 },
                    new UpdateQuoteItem { RequestItemId = Receiver.Id, UnitPricePiastres = 850_000 }
                ]
            });
            await Quotes.Service.AcceptAsync(Request.Id, quote.Id, CoordinatorId, "coordinator");
        }

        public Task<IReadOnlyList<QuoteSummaryResponse>> QueueAsync(
            Guid actorId, string role, QuotationQueue queue, QuotationQueueQuery? query = null) =>
            Quotes.Service.ListQueueAsync(actorId, role, queue, query ?? new QuotationQueueQuery());
    }
}
