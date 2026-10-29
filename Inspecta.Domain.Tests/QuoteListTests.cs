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

    private sealed class Fixture
    {
        public Fixture()
        {
            var inner = new QuotationTests.Fixture();
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

        public Task<IReadOnlyList<QuoteSummaryResponse>> ListAsync(Guid actorId, string role, QuoteListQuery query) =>
            Service.ListVisibleAsync(actorId, role, query);
    }
}
