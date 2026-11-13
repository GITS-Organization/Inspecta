using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>An in-memory list of invoices for the tests that use fake repositories.</summary>
internal sealed class FakeInvoices : IInvoiceRepository
{
    public List<Invoice> Items { get; } = [];
    public int Saves { get; private set; }

    public Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        Items.Add(invoice);
        return Task.CompletedTask;
    }

    public Task<Invoice?> GetByIdAsync(Guid id, bool tracking = false, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

    public Task<Invoice?> GetByQuoteIdAsync(Guid quoteId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.SingleOrDefault(item => item.QuoteId == quoteId));

    public Task<InvoicePage> ListAsync(InvoiceListFilter filter, CancellationToken cancellationToken = default) =>
        Task.FromResult(new InvoicePage(Items.Where(item => filter.RequestId is null || item.RequestId == filter.RequestId).ToList(), Items.Count));

    public Task<IReadOnlyList<RevenueRow>> RevenueAsync(DateOnly from, DateOnly to, Guid? managerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RevenueRow>>(Items
            .SelectMany(invoice => invoice.Payments.Select(payment => new RevenueRow(invoice.ClientId, invoice.ClientName, payment.PaidOn, payment.AmountPiastres)))
            .Where(row => row.PaidOn >= from && row.PaidOn <= to).ToList());

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.CompletedTask;
    }
}

/// <summary>An invoice is made, once, when a quotation is accepted, in the same save as the acceptance.</summary>
public sealed class InvoiceCreationTests
{
    private static readonly Guid CoordinatorId = QuotationTests.CoordinatorId;

    private static UpdateQuoteRequest Priced(QuotationTests.Fixture fixture, decimal? discount = null) => new()
    {
        Items =
        [
            new UpdateQuoteItem { RequestItemId = fixture.Crane.Id, UnitPricePiastres = 1_200_000 },
            new UpdateQuoteItem { RequestItemId = fixture.Receiver.Id, UnitPricePiastres = 850_000 }
        ],
        DiscountPercent = discount
    };

    private static async Task<(QuotationTests.Fixture Fixture, Guid QuoteId)> DraftAsync(decimal? discount = null)
    {
        var fixture = new QuotationTests.Fixture();
        var quote = await fixture.Service.CreateAsync(fixture.Request.Id, CoordinatorId, "coordinator", new CreateQuoteRequest());
        await fixture.Service.UpdateAsync(fixture.Request.Id, quote.Id, CoordinatorId, "coordinator", Priced(fixture, discount));
        return (fixture, quote.Id);
    }

    [Fact]
    public async Task Accept_creates_one_invoice_with_the_quote_numbers()
    {
        var (fixture, quoteId) = await DraftAsync();

        await fixture.Service.AcceptAsync(fixture.Request.Id, quoteId, CoordinatorId, "coordinator");

        var invoice = Assert.Single(fixture.Invoices.Items);
        var quote = (await fixture.Quotes.GetByIdAsync(quoteId))!;
        Assert.Equal(quote.Id, invoice.QuoteId);
        Assert.Equal(fixture.Request.Id, invoice.RequestId);
        Assert.Equal(quote.TotalWithVatPiastres, invoice.TotalPiastres);
        Assert.Equal(1_200_000 + 4 * 850_000, invoice.TotalPiastres);
        Assert.Equal(fixture.Request.ManagerId, invoice.ManagerId);
        Assert.Equal(CoordinatorId, invoice.CreatedBy);
        Assert.Equal(InvoiceStage.Open, invoice.Stage);
        Assert.Equal(InvoiceCalendarToday(), invoice.AcceptedOn);
    }

    [Fact]
    public async Task Record_decision_accepted_creates_one_invoice()
    {
        var (fixture, quoteId) = await DraftAsync();
        await fixture.Service.SendAsync(fixture.Request.Id, quoteId, CoordinatorId, "coordinator");

        await fixture.Service.RecordDecisionAsync(
            fixture.Request.Id, quoteId, CoordinatorId, "coordinator", new QuoteDecisionRequest { Decision = QuoteStatus.Accepted });

        Assert.Single(fixture.Invoices.Items);
    }

    [Fact]
    public async Task Accepting_twice_never_creates_a_second_invoice()
    {
        var (fixture, quoteId) = await DraftAsync();
        await fixture.Service.AcceptAsync(fixture.Request.Id, quoteId, CoordinatorId, "coordinator");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.AcceptAsync(fixture.Request.Id, quoteId, CoordinatorId, "coordinator"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RecordDecisionAsync(
            fixture.Request.Id, quoteId, CoordinatorId, "coordinator", new QuoteDecisionRequest { Decision = QuoteStatus.Accepted }));

        Assert.Single(fixture.Invoices.Items);
    }

    [Fact]
    public async Task A_quote_with_nothing_to_pay_creates_no_invoice_and_the_accept_still_succeeds()
    {
        var (fixture, quoteId) = await DraftAsync(discount: 100m);

        var accepted = await fixture.Service.AcceptAsync(fixture.Request.Id, quoteId, CoordinatorId, "coordinator");

        Assert.Equal(QuoteStatus.Accepted, accepted.Status);
        Assert.Empty(fixture.Invoices.Items);
    }

    [Fact]
    public async Task A_rejected_decision_creates_no_invoice()
    {
        var (fixture, quoteId) = await DraftAsync();
        await fixture.Service.SendAsync(fixture.Request.Id, quoteId, CoordinatorId, "coordinator");

        await fixture.Service.RecordDecisionAsync(
            fixture.Request.Id, quoteId, CoordinatorId, "coordinator", new QuoteDecisionRequest { Decision = QuoteStatus.Rejected, Note = "Too dear" });

        Assert.Empty(fixture.Invoices.Items);
    }

    [Fact]
    public async Task The_invoice_is_added_before_the_one_save_that_closes_the_quote()
    {
        var (fixture, quoteId) = await DraftAsync();
        var invoicesAtSave = -1;
        fixture.Quotes.OnSave = () => invoicesAtSave = fixture.Invoices.Items.Count;

        await fixture.Service.AcceptAsync(fixture.Request.Id, quoteId, CoordinatorId, "coordinator");

        Assert.Equal(1, invoicesAtSave);
        Assert.Equal(0, fixture.Invoices.Saves);
    }

    private static DateOnly InvoiceCalendarToday() => Inspecta.Application.Services.InvoiceCalendar.Today();
}
