namespace Inspecta.Application.Contracts.Invoices;

/// <summary>
/// An invoice in a list or at the top of its page. Money is in piastres. <c>stage</c> is <c>open</c> (the coordinator follows it),
/// <c>with_finance</c> (the financial manager follows it) or <c>closed</c>; <c>paymentState</c> is <c>unpaid</c>, <c>partial</c> or <c>paid</c>.
/// It carries no detail of the inspection work.
/// </summary>
public sealed record InvoiceSummary(
    Guid Id,
    string Number,
    string ClientName,
    Guid? ClientId,
    string? QuotationReference,
    Guid RequestId,
    long TotalPiastres,
    long PaidPiastres,
    long BalancePiastres,
    string PaymentState,
    string Stage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClientEmailedAt,
    DateTimeOffset? HandedOverAt,
    DateTimeOffset? ClosedAt);

/// <summary>One price line, as the quotation had it when it was accepted.</summary>
public sealed record InvoiceLine(string Type, string? Description, int Quantity, long? UnitPricePiastres, string? PricingUnit, long TotalPiastres);

/// <summary>A payment (positive) or the reversal of one (negative, pointing at it with <c>reversesPaymentId</c>).</summary>
public sealed record InvoicePaymentRow(
    Guid Id,
    long AmountPiastres,
    DateOnly PaidOn,
    string? Label,
    Guid? ReversesPaymentId,
    string? Reason,
    Guid RecordedBy,
    string? RecordedByName,
    DateTimeOffset RecordedAt);

/// <summary>One line of the history, oldest first: <c>created</c>, <c>payment</c>, <c>payment_reversed</c>, <c>emailed</c>, <c>handed_over</c>, <c>closed</c>.</summary>
public sealed record InvoiceHistoryRow(DateTimeOffset At, string Kind, Guid? ActorId, string? ActorName, string? Note, long? AmountPiastres);

/// <summary>
/// An invoice with its frozen numbers, lines, payments and history. <c>actions</c> lists what the caller can do with it now:
/// <c>record_payment</c>, <c>reverse_payment</c>, <c>email_client</c>, <c>hand_over</c>, <c>close</c>, <c>close_manually</c>.
/// </summary>
public sealed record InvoiceDetail(
    InvoiceSummary Summary,
    long SubtotalPiastres,
    decimal DiscountPercent,
    long DiscountPiastres,
    decimal VatPercent,
    long VatPiastres,
    IReadOnlyList<InvoiceLine> Lines,
    IReadOnlyList<InvoicePaymentRow> Payments,
    IReadOnlyList<InvoiceHistoryRow> History,
    IReadOnlyList<string> Actions);

/// <summary>Money received: how much (piastres), the day it arrived, and an optional label such as "Advance".</summary>
public sealed record RecordPaymentRequest(long AmountPiastres, DateOnly PaidOn, string? Label = null);

public sealed record ReversePaymentRequest(string Reason);

/// <summary>Without a <c>reason</c> the invoice closes only at a balance of zero; with one it closes whatever is still owed.</summary>
public sealed record CloseInvoiceRequest(string? Reason = null);

/// <summary>Filters and paging for the invoice list.</summary>
public sealed record InvoiceListQuery(
    string? Stage = null,
    string? PaymentState = null,
    string? Search = null,
    Guid? RequestId = null,
    Guid? ManagerId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 50,
    Guid? ReviewerId = null);

/// <summary>
/// The money received from <c>from</c> to <c>to</c> (both days included, Cairo), VAT included, by the day each payment was received.
/// A reversal counts as negative money on the day it was recorded.
/// </summary>
public sealed record RevenueResponse(
    DateOnly From,
    DateOnly To,
    long TotalPiastres,
    IReadOnlyList<RevenueByMonth> ByMonth,
    IReadOnlyList<RevenueByClient> ByClient);

/// <summary><c>month</c> is <c>yyyy-MM</c>. Months with no payment are left out.</summary>
public sealed record RevenueByMonth(string Month, long AmountPiastres);

/// <summary>One client's share, biggest first: grouped by client id when the request had one, else by the name on the invoice.</summary>
public sealed record RevenueByClient(Guid? ClientId, string ClientName, long AmountPiastres);
