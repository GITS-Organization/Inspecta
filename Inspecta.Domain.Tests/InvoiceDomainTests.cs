using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Domain.Exceptions;

namespace Inspecta.Domain.Tests;

/// <summary>The rules of an invoice: frozen numbers, payments that are only ever added, the hand-over to finance and the close.</summary>
public sealed class InvoiceDomainTests
{
    private static readonly DateOnly AcceptedOn = new(2026, 11, 1);
    private static readonly DateOnly Today = new(2026, 11, 13);
    private static readonly Guid Actor = Guid.NewGuid();

    private static (Quote Quote, InspectionRequest Request) AcceptedQuote(
        long unitPrice = 1_000_000, int quantity = 1, decimal vat = 0m, decimal discount = 0m, Guid? managerId = null)
    {
        var request = InspectionRequest.Create(
            "Client X", "Cairo", Guid.NewGuid(), [RequestItem.Create("Crane", 1)], Guid.NewGuid(), managerId: managerId ?? Guid.NewGuid());
        var quote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(10), Actor, clientName: "Client X");
        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", quantity, unitPrice, "Per Visit", "Crane inspection", "PR-1")], null, null);
        if (discount > 0) quote.SetDiscount(discount);
        if (vat > 0) quote.SetVat(vat);
        quote.Send();
        quote.RecordClientDecision(QuoteStatus.Accepted);
        return (quote, request);
    }

    private static Invoice NewInvoice(long unitPrice = 1_000_000, decimal vat = 0m)
    {
        var (quote, request) = AcceptedQuote(unitPrice, vat: vat);
        return Invoice.CreateFor(quote, request, Actor, AcceptedOn)!;
    }

    private static Invoice WithFinance(long unitPrice = 1_000_000)
    {
        var invoice = NewInvoice(unitPrice);
        invoice.HandOver(Actor);
        return invoice;
    }

    private static string CodeOf(Action action) => Assert.Throws<InvoiceRuleException>(action).Code;

    [Fact]
    public void CreateFor_copies_the_accepted_numbers()
    {
        var (quote, request) = AcceptedQuote(1_000_000, vat: 14m, discount: 10m);

        var invoice = Invoice.CreateFor(quote, request, Actor, AcceptedOn)!;

        Assert.Equal(request.ManagerId, invoice.ManagerId);
        Assert.Equal(request.Id, invoice.RequestId);
        Assert.Equal(quote.Id, invoice.QuoteId);
        Assert.Equal("Client X", invoice.ClientName);
        Assert.Equal(quote.RequestNumber, invoice.QuotationReference);
        Assert.Equal(quote.SubtotalPiastres, invoice.SubtotalPiastres);
        Assert.Equal(quote.DiscountPercent, invoice.DiscountPercent);
        Assert.Equal(quote.DiscountPiastres, invoice.DiscountPiastres);
        Assert.Equal(quote.AmountPiastres, invoice.AmountPiastres);
        Assert.Equal(quote.VatPercent, invoice.VatPercent);
        Assert.Equal(quote.VatPiastres, invoice.VatPiastres);
        Assert.Equal(quote.TotalWithVatPiastres, invoice.TotalPiastres);
        Assert.Equal(1_026_000, invoice.TotalPiastres);
        Assert.Single(invoice.GetItems());
        Assert.Equal(InvoiceStage.Open, invoice.Stage);
        Assert.Matches($"^INV-{DateTimeOffset.UtcNow.Year}-[0-9A-F]{{6}}$", invoice.Number);
        Assert.Equal(InvoiceEventKind.Created, Assert.Single(invoice.Events).Kind);
        Assert.Equal(invoice.TotalPiastres, invoice.BalancePiastres);
        Assert.Equal("unpaid", invoice.PaymentState);
    }

    [Fact]
    public void CreateFor_returns_null_for_placeholder_and_for_zero_total()
    {
        var request = InspectionRequest.Create("Client X", "Cairo", null, [RequestItem.Create("Crane", 1)], Guid.NewGuid(), managerId: Guid.NewGuid());
        var placeholder = Quote.CreateAccepted(request.Id, Actor, "MD", "Client X");
        Assert.Null(Invoice.CreateFor(placeholder, request, Actor, AcceptedOn));

        var (full, fullRequest) = AcceptedQuote(discount: 100m);
        Assert.Equal(0, full.TotalWithVatPiastres);
        Assert.Null(Invoice.CreateFor(full, fullRequest, Actor, AcceptedOn));
    }

    [Fact]
    public void CreateFor_refuses_a_quote_that_is_not_accepted()
    {
        var request = InspectionRequest.Create("Client X", "Cairo", null, [RequestItem.Create("Crane", 1)], Guid.NewGuid(), managerId: Guid.NewGuid());
        var quote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(10), Actor);
        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, 1_000, "Per Visit")], null, null);

        Assert.Throws<InvalidOperationException>(() => Invoice.CreateFor(quote, request, Actor, AcceptedOn));
    }

    [Fact]
    public void RecordPayment_adds_to_paid_and_reduces_balance()
    {
        var invoice = NewInvoice();

        var first = invoice.RecordPayment(200_000, AcceptedOn.AddDays(2), "Advance", Actor, Today);
        invoice.RecordPayment(500_000, Today, null, Actor, Today);

        Assert.Equal(700_000, invoice.PaidPiastres);
        Assert.Equal(300_000, invoice.BalancePiastres);
        Assert.Equal("partial", invoice.PaymentState);
        Assert.Equal(2, invoice.Payments.Count);
        Assert.Equal("Advance", first.Label);
        Assert.Equal(Actor, first.RecordedBy);
        Assert.Equal(invoice.ManagerId, first.ManagerId);

        invoice.RecordPayment(300_000, Today, null, Actor, Today);
        Assert.Equal("paid", invoice.PaymentState);
        Assert.Equal(0, invoice.BalancePiastres);
    }

    [Fact]
    public void RecordPayment_over_the_balance_throws_overpayment()
    {
        var invoice = NewInvoice();
        invoice.RecordPayment(700_000, Today, null, Actor, Today);

        Assert.Equal("overpayment", CodeOf(() => invoice.RecordPayment(300_001, Today, null, Actor, Today)));
        Assert.Single(invoice.Payments);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void RecordPayment_needs_a_positive_amount(long amount) =>
        Assert.Equal("payment_amount_invalid", CodeOf(() => NewInvoice().RecordPayment(amount, Today, null, Actor, Today)));

    [Fact]
    public void RecordPayment_with_future_date_or_before_acceptance_throws_payment_date_invalid()
    {
        var invoice = NewInvoice();

        Assert.Equal("payment_date_invalid", CodeOf(() => invoice.RecordPayment(1, Today.AddDays(1), null, Actor, Today)));
        Assert.Equal("payment_date_invalid", CodeOf(() => invoice.RecordPayment(1, AcceptedOn.AddDays(-1), null, Actor, Today)));
        invoice.RecordPayment(1, AcceptedOn, null, Actor, Today);
        invoice.RecordPayment(1, Today, null, Actor, Today);
    }

    [Fact]
    public void RecordPayment_on_closed_invoice_throws_invoice_closed()
    {
        var invoice = WithFinance(1_000);
        invoice.RecordPayment(1_000, Today, null, Actor, Today);
        invoice.Close(Actor);

        Assert.Equal("invoice_closed", CodeOf(() => invoice.RecordPayment(1, Today, null, Actor, Today)));
    }

    [Fact]
    public void ReversePayment_adds_a_negative_row_dated_today_and_keeps_the_original()
    {
        var invoice = NewInvoice();
        var payment = invoice.RecordPayment(200_000, AcceptedOn.AddDays(1), null, Actor, Today);

        var reversal = invoice.ReversePayment(payment.Id, "Typed the wrong client", Actor, Today);

        Assert.Equal(-200_000, reversal.AmountPiastres);
        Assert.Equal(Today, reversal.PaidOn);
        Assert.Equal(payment.Id, reversal.ReversesPaymentId);
        Assert.Equal("Typed the wrong client", reversal.Reason);
        Assert.Equal(2, invoice.Payments.Count);
        Assert.Equal(200_000, invoice.Payments.Single(item => item.Id == payment.Id).AmountPiastres);
        Assert.Equal(0, invoice.PaidPiastres);
        Assert.Equal(invoice.TotalPiastres, invoice.BalancePiastres);
    }

    [Fact]
    public void ReversePayment_twice_or_of_a_reversal_or_of_a_foreign_payment_throws()
    {
        var invoice = NewInvoice();
        var payment = invoice.RecordPayment(200_000, Today, null, Actor, Today);
        var reversal = invoice.ReversePayment(payment.Id, "wrong", Actor, Today);

        Assert.Equal("already_reversed", CodeOf(() => invoice.ReversePayment(payment.Id, "again", Actor, Today)));
        Assert.Equal("not_a_payment", CodeOf(() => invoice.ReversePayment(reversal.Id, "undo", Actor, Today)));
        Assert.Equal("not_a_payment", CodeOf(() => invoice.ReversePayment(Guid.NewGuid(), "ghost", Actor, Today)));
    }

    [Fact]
    public void ReversePayment_requires_a_reason()
    {
        var invoice = NewInvoice();
        var payment = invoice.RecordPayment(200_000, Today, null, Actor, Today);

        Assert.Equal("reason_required", CodeOf(() => invoice.ReversePayment(payment.Id, "  ", Actor, Today)));
    }

    [Fact]
    public void ReversePayment_on_a_closed_invoice_needs_allowClosed()
    {
        var invoice = WithFinance(1_000);
        var payment = invoice.RecordPayment(1_000, Today, null, Actor, Today);
        invoice.Close(Actor);

        Assert.Equal("invoice_closed", CodeOf(() => invoice.ReversePayment(payment.Id, "oops", Actor, Today)));
        invoice.ReversePayment(payment.Id, "oops", Actor, Today, allowClosed: true);
        Assert.Equal(InvoiceStage.Closed, invoice.Stage);
        Assert.Equal(1_000, invoice.BalancePiastres);
    }

    [Fact]
    public void HandOver_moves_open_to_with_finance_once()
    {
        var invoice = NewInvoice();

        invoice.HandOver(Actor);

        Assert.Equal(InvoiceStage.WithFinance, invoice.Stage);
        Assert.NotNull(invoice.HandedOverAt);
        Assert.Equal(Actor, invoice.HandedOverBy);
        Assert.Contains(invoice.Events, item => item.Kind == InvoiceEventKind.HandedOver);
        Assert.Equal("wrong_stage", CodeOf(() => invoice.HandOver(Actor)));
    }

    [Fact]
    public void MarkEmailed_records_the_time_and_an_event_any_number_of_times()
    {
        var invoice = NewInvoice();

        invoice.MarkEmailed(Actor);
        invoice.MarkEmailed(Actor);

        Assert.NotNull(invoice.ClientEmailedAt);
        Assert.Equal(2, invoice.Events.Count(item => item.Kind == InvoiceEventKind.Emailed));
    }

    [Fact]
    public void Close_requires_zero_balance_and_with_finance()
    {
        var invoice = NewInvoice(1_000);
        Assert.Equal("wrong_stage", CodeOf(() => invoice.Close(Actor)));

        invoice.HandOver(Actor);
        Assert.Equal("balance_not_zero", CodeOf(() => invoice.Close(Actor)));

        invoice.RecordPayment(1_000, Today, null, Actor, Today);
        invoice.Close(Actor);

        Assert.Equal(InvoiceStage.Closed, invoice.Stage);
        Assert.False(invoice.ClosedWithBalance);
        Assert.NotNull(invoice.ClosedAt);
        Assert.Equal(Actor, invoice.ClosedBy);
        Assert.Equal("invoice_closed", CodeOf(() => invoice.Close(Actor)));
    }

    [Fact]
    public void CloseManually_ignores_the_balance_and_needs_a_reason()
    {
        var invoice = NewInvoice();
        invoice.RecordPayment(200_000, Today, null, Actor, Today);

        Assert.Equal("reason_required", CodeOf(() => invoice.CloseManually(Actor, " ")));
        invoice.CloseManually(Actor, "Job was declined; the advance was settled with the client");

        Assert.Equal(InvoiceStage.Closed, invoice.Stage);
        Assert.True(invoice.ClosedWithBalance);
        Assert.Equal("Job was declined; the advance was settled with the client", invoice.Events.Last(item => item.Kind == InvoiceEventKind.Closed).Note);
    }

    [Fact]
    public void Every_step_is_in_the_history_with_who_and_when()
    {
        var invoice = NewInvoice(1_000);
        invoice.MarkEmailed(Actor);
        invoice.HandOver(Actor);
        invoice.RecordPayment(1_000, Today, null, Actor, Today);
        invoice.Close(Actor);

        Assert.Equal(
            [InvoiceEventKind.Created, InvoiceEventKind.Emailed, InvoiceEventKind.HandedOver, InvoiceEventKind.Closed],
            invoice.Events.OrderBy(item => item.At).Select(item => item.Kind).ToArray());
        Assert.All(invoice.Events, item => Assert.Equal(invoice.ManagerId, item.ManagerId));
    }
}
