using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

public sealed class QuoteListTests
{
    private static readonly Guid ManagerId = QuotationTests.ManagerId;
    private static readonly Guid CoordinatorId = QuotationTests.CoordinatorId;
    private static readonly AppUser Inspector = AppUser.Create("inspector@example.com", "Eng. Karim", UserRole.Inspector, managerId: ManagerId);

    [Fact]
    public async Task ADraftShowsUpInTheCoordinatorsListReadyToWorkOn()
    {
        var fixture = new Fixture();
        await fixture.OpenDraftAsync();

        var row = Assert.Single(await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery()));

        Assert.Equal(QuoteStage.Draft, row.Stage);
        Assert.Equal(fixture.Request.Id, row.RequestId);
        Assert.Equal(JobStatus.QuoteDraft, row.RequestStatus);
        Assert.Equal("Al Noor Heavy Industries Ltd.", row.ClientName);
        Assert.Equal("Industrial City", row.Location);
        Assert.Equal(5, row.TotalUnits);
        Assert.Equal(2, row.MissingPrices);
        Assert.True(row.CanEdit);
        Assert.True(row.IsCurrent);
    }

    [Fact]
    public async Task TheStageFilterSeparatesDraftsFromSentAndClosedQuotations()
    {
        var fixture = new Fixture();
        var quote = await fixture.OpenDraftAsync();

        Assert.Single(await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery(Stage: QuoteStage.Draft)));
        Assert.Empty(await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery(Stage: QuoteStage.Sent)));

        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", fixture.Prices(1_200_000, 850_000));
        await fixture.Service.SendAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");

        Assert.Empty(await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery(Stage: QuoteStage.Draft)));
        var sent = Assert.Single(await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery(Stage: QuoteStage.Sent)));
        Assert.False(sent.CanEdit);
        Assert.Equal(0, sent.MissingPrices);
        Assert.Equal(1_200_000 + 4 * 850_000, sent.AmountPiastres);

        await fixture.Service.AcceptAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator");

        Assert.Empty(await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery(Stage: QuoteStage.Sent)));
        Assert.Equal(QuoteStage.Accepted,
            Assert.Single(await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery(Stage: QuoteStage.Accepted))).Stage);
        Assert.Empty(await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery(Stage: QuoteStage.Rejected)));
    }

    [Fact]
    public async Task OldRevisionsAreHiddenUnlessAsked()
    {
        var fixture = new Fixture();
        var first = await fixture.OpenDraftAsync();
        await fixture.Service.UpdateAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator", fixture.Prices(1_200_000, 850_000));
        await fixture.Service.SendAsync(fixture.Request.Id, first.Id, CoordinatorId, "coordinator");
        var second = await fixture.Service.CreateAsync(
            fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());

        var current = Assert.Single(await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery()));
        Assert.Equal(second.Id, current.Id);
        Assert.Equal(2, current.Version);

        var all = await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery(IncludeSuperseded: true));
        Assert.Equal(2, all.Count);
        Assert.False(all.Single(row => row.Id == first.Id).IsCurrent);
    }

    [Fact]
    public async Task AnotherCoordinatorDoesNotSeeTheQuotationsOfAnEmailRequestTheyDidNotReceive()
    {
        var fixture = new Fixture();
        await fixture.OpenDraftAsync();

        Assert.Empty(await fixture.ListAsync(Guid.NewGuid(), "coordinator", new QuoteListQuery()));
    }

    [Fact]
    public async Task ManagersSeeTheTeamsQuotationsButCannotEditThem()
    {
        var fixture = new Fixture();
        await fixture.OpenDraftAsync();

        var row = Assert.Single(await fixture.ListAsync(ManagerId, "technical_manager", new QuoteListQuery()));

        Assert.False(row.CanEdit);
    }

    [Fact]
    public async Task InspectorsNeverSeeQuotations()
    {
        var fixture = new Fixture();
        await fixture.OpenDraftAsync();

        Assert.Empty(await fixture.ListAsync(Guid.NewGuid(), "inspector", new QuoteListQuery()));
    }

    [Fact]
    public async Task PagingIsClampedAndAppliedToTheRepositoryQuery()
    {
        var fixture = new Fixture();
        await fixture.OpenDraftAsync();

        await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery(Page: 0, PageSize: 10_000, Search: "  noor "));

        var filter = fixture.Quotes.LastFilter!;
        Assert.Equal(0, filter.Skip);
        Assert.Equal(QuoteListQuery.MaxPageSize, filter.Take);
        Assert.Equal("noor", filter.Search);
        Assert.Equal(CoordinatorId, filter.CoordinatorId);
        Assert.True(filter.CurrentOnly);
    }

    [Fact]
    public async Task AnAcceptedQuotationWaitsForAnInspectorAndThenShowsWhoGotIt()
    {
        var fixture = new Fixture();
        await fixture.AcceptAndAssignAsync();

        var waiting = Assert.Single(await fixture.ListAsync(
            CoordinatorId, "coordinator", new QuoteListQuery(Stage: QuoteStage.Accepted, Assigned: false)));
        Assert.Null(waiting.InspectorId);
        Assert.Empty(await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery(Assigned: true)));

        fixture.Request.AssignInspector(Inspector.Id, DateTimeOffset.UtcNow.AddDays(2), CoordinatorId);

        var given = Assert.Single(await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery(Assigned: true)));
        Assert.Equal(Inspector.Id, given.InspectorId);
        Assert.Equal("Eng. Karim", given.InspectorName);
        Assert.NotNull(given.ScheduledAt);
        Assert.Equal(JobStatus.Assigned, given.RequestStatus);
        Assert.Equal(QuoteStage.Accepted, given.Stage);
        Assert.Empty(await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery(Assigned: false)));
    }

    [Fact]
    public async Task TheListCanBeNarrowedToOneInspector()
    {
        var fixture = new Fixture();
        await fixture.AcceptAndAssignAsync(Inspector.Id);

        Assert.Single(await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery(InspectorId: Inspector.Id)));
        Assert.Empty(await fixture.ListAsync(CoordinatorId, "coordinator", new QuoteListQuery(InspectorId: Guid.NewGuid())));
    }

    [Fact]
    public async Task AnInspectorSeesOnlyTheirOwnAcceptedQuotationsAndNoPrices()
    {
        var fixture = new Fixture();
        await fixture.AcceptAndAssignAsync(Inspector.Id);

        var row = Assert.Single(await fixture.ListAsync(Inspector.Id, "inspector", new QuoteListQuery()));

        Assert.Equal(fixture.Request.Id, row.RequestId);
        Assert.Equal("Al Noor Heavy Industries Ltd.", row.ClientName);
        Assert.Equal("Industrial City", row.Location);
        Assert.Equal(5, row.TotalUnits);
        Assert.Null(row.AmountPiastres);
        Assert.Equal(0, row.MissingPrices);
        Assert.False(row.CanEdit);
        Assert.Empty(await fixture.ListAsync(Guid.NewGuid(), "inspector", new QuoteListQuery()));
        // Asking for someone else's work does not widen what an inspector can see.
        Assert.Empty(await fixture.ListAsync(Guid.NewGuid(), "inspector", new QuoteListQuery(InspectorId: Inspector.Id, Assigned: true)));
    }

    [Fact]
    public async Task AnInspectorDoesNotSeeAQuotationThatIsNotAcceptedYet()
    {
        var fixture = new Fixture();
        await fixture.OpenDraftAsync();

        Assert.Empty(await fixture.ListAsync(Inspector.Id, "inspector", new QuoteListQuery()));
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            var inner = new QuotationTests.Fixture(extraUsers: [Inspector]);
            Request = inner.Request;
            Crane = inner.Crane;
            Receiver = inner.Receiver;
            Service = inner.Service;
            Quotes = inner.Quotes;
            Quotes.RequestOf = id => id == Request.Id ? Request : null;
        }

        public InspectionRequest Request { get; }
        public RequestItem Crane { get; }
        public RequestItem Receiver { get; }
        public QuoteService Service { get; }
        public QuotationTests.FakeQuotes Quotes { get; }

        public Task<QuoteResponse> OpenDraftAsync() =>
            Service.CreateAsync(Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());

        public UpdateQuoteRequest Prices(long crane, long receiver) => new()
        {
            Items =
            [
                new UpdateQuoteItem { RequestItemId = Crane.Id, UnitPricePiastres = crane },
                new UpdateQuoteItem { RequestItemId = Receiver.Id, UnitPricePiastres = receiver }
            ]
        };

        public async Task AcceptAndAssignAsync(Guid? inspectorId = null)
        {
            var quote = await OpenDraftAsync();
            await Service.UpdateAsync(Request.Id, quote.Id, CoordinatorId, "coordinator", Prices(1_200_000, 850_000));
            await Service.AcceptAsync(Request.Id, quote.Id, CoordinatorId, "coordinator");
            if (inspectorId is Guid id) Request.AssignInspector(id, DateTimeOffset.UtcNow.AddDays(2), CoordinatorId);
        }

        public Task<IReadOnlyList<QuoteSummaryResponse>> ListAsync(Guid actorId, string role, QuoteListQuery query) =>
            Service.ListVisibleAsync(actorId, role, query);
    }
}
