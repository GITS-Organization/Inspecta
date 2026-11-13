using System.Text.Json;
using Inspecta.Domain.Enums;
using Inspecta.Domain.Exceptions;

namespace Inspecta.Domain.Entities;

/// <summary>
/// What a client owes for an accepted quotation: the final numbers copied from it (so they never change), the money received so far
/// (payments are only ever added) and where the invoice is (<see cref="InvoiceStage"/>). It is its own thing, separate from the request's
/// status. The official e-invoice is made elsewhere and is not part of it.
/// </summary>
public sealed class Invoice
{
    public const int MaxNoteLength = 500;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly List<InvoicePayment> payments = [];
    private readonly List<InvoiceEvent> events = [];

    private Invoice() { }

    public Guid Id { get; private set; }

    /// <summary>The company (tenant) the invoice belongs to.</summary>
    public Guid ManagerId { get; private set; }
    public Guid RequestId { get; private set; }

    /// <summary>The accepted quotation it came from: one invoice per quotation.</summary>
    public Guid QuoteId { get; private set; }
    public string Number { get; private set; } = string.Empty;
    public Guid? ClientId { get; private set; }
    public string ClientName { get; private set; } = string.Empty;
    public string? QuotationReference { get; private set; }
    public string ItemsJson { get; private set; } = "[]";
    public long SubtotalPiastres { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public long DiscountPiastres { get; private set; }

    /// <summary>The total after the discount, before VAT.</summary>
    public long AmountPiastres { get; private set; }
    public decimal VatPercent { get; private set; }
    public long VatPiastres { get; private set; }

    /// <summary>What the client pays: the amount plus the VAT.</summary>
    public long TotalPiastres { get; private set; }

    /// <summary>The day (Cairo) the quotation was accepted: no payment can be dated before it.</summary>
    public DateOnly AcceptedOn { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public InvoiceStage Stage { get; private set; }
    public DateTimeOffset? ClientEmailedAt { get; private set; }
    public DateTimeOffset? HandedOverAt { get; private set; }
    public Guid? HandedOverBy { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedBy { get; private set; }

    /// <summary>True when it was closed by hand with money still owed.</summary>
    public bool ClosedWithBalance { get; private set; }

    /// <summary>Changes with every rule method, so two changes made at the same moment cannot both win.</summary>
    public long RowVersion { get; private set; }

    public IReadOnlyList<InvoicePayment> Payments => payments;
    public IReadOnlyList<InvoiceEvent> Events => events;

    public long PaidPiastres => payments.Sum(item => item.AmountPiastres);
    public long BalancePiastres => TotalPiastres - PaidPiastres;

    public string PaymentState => BalancePiastres <= 0 && TotalPiastres > 0 ? "paid" : PaidPiastres > 0 ? "partial" : "unpaid";

    public IReadOnlyList<QuoteItem> GetItems() =>
        JsonSerializer.Deserialize<List<QuoteItem>>(ItemsJson, JsonOptions)
        ?? throw new JsonException("Invoice items must be a JSON array.");

    /// <summary>
    /// The invoice of an accepted quotation, with its numbers frozen. <c>null</c> when there is nothing to invoice: a placeholder
    /// quotation (man day, direct certificate), a total of zero, or a request that belongs to no company.
    /// </summary>
    public static Invoice? CreateFor(Quote quote, InspectionRequest request, Guid actorId, DateOnly acceptedOn, string? note = null)
    {
        ArgumentNullException.ThrowIfNull(quote);
        ArgumentNullException.ThrowIfNull(request);
        if (actorId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorId));
        if (quote.Status != QuoteStatus.Accepted)
            throw new InvalidOperationException("An invoice is made only from an accepted quotation.");
        if (quote.IsPlaceholder || quote.TotalWithVatPiastres <= 0 || request.ManagerId is not Guid managerId) return null;

        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        var invoice = new Invoice
        {
            Id = id,
            ManagerId = managerId,
            RequestId = request.Id,
            QuoteId = quote.Id,
            Number = $"INV-{now.Year}-{id.ToString("N")[..6].ToUpperInvariant()}",
            ClientId = request.ClientId,
            ClientName = string.IsNullOrWhiteSpace(quote.ClientName) ? request.ClientName : quote.ClientName,
            QuotationReference = quote.RequestNumber,
            ItemsJson = quote.ItemsJson,
            SubtotalPiastres = quote.SubtotalPiastres,
            DiscountPercent = quote.DiscountPercent,
            DiscountPiastres = quote.DiscountPiastres,
            AmountPiastres = quote.AmountPiastres,
            VatPercent = quote.VatPercent,
            VatPiastres = quote.VatPiastres,
            TotalPiastres = quote.TotalWithVatPiastres,
            AcceptedOn = acceptedOn,
            CreatedAt = now,
            CreatedBy = actorId,
            Stage = InvoiceStage.Open
        };
        invoice.events.Add(InvoiceEvent.Create(invoice, InvoiceEventKind.Created, actorId, note));
        return invoice;
    }

    /// <summary>Records money received. Not after the invoice is closed, not above what is still owed, and dated from the acceptance up to today.</summary>
    public InvoicePayment RecordPayment(long amountPiastres, DateOnly paidOn, string? label, Guid actorId, DateOnly today)
    {
        EnsureNotClosed();
        if (amountPiastres <= 0) throw new InvoiceRuleException("payment_amount_invalid", "A payment must be more than zero.");
        if (paidOn > today || paidOn < AcceptedOn)
            throw new InvoiceRuleException("payment_date_invalid", "The payment date must be between the day the quotation was accepted and today.");
        if (amountPiastres > BalancePiastres)
            throw new InvoiceRuleException("overpayment", "The payment is more than what is still owed on the invoice.");
        if (label is not null && label.Trim().Length > InvoicePayment.MaxLabelLength)
            throw new InvoiceRuleException("payment_label_too_long", $"The label can be at most {InvoicePayment.MaxLabelLength} characters.");

        var payment = InvoicePayment.Create(this, amountPiastres, paidOn, label, actorId);
        payments.Add(payment);
        RowVersion++;
        return payment;
    }

    /// <summary>
    /// Undoes one payment with a negative row dated today; the payment itself stays. Only a payment (not a reversal) of this invoice, once.
    /// <paramref name="allowClosed"/> is for the platform owner, who may correct a closed invoice.
    /// </summary>
    public InvoicePayment ReversePayment(Guid paymentId, string reason, Guid actorId, DateOnly today, bool allowClosed = false)
    {
        if (!allowClosed) EnsureNotClosed();
        var original = payments.SingleOrDefault(item => item.Id == paymentId);
        if (original is null || original.AmountPiastres <= 0)
            throw new InvoiceRuleException("not_a_payment", "Only a payment of this invoice can be reversed.");
        if (payments.Any(item => item.ReversesPaymentId == paymentId))
            throw new InvoiceRuleException("already_reversed", "This payment was already reversed.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvoiceRuleException("reason_required", "A reason is required.");
        if (reason.Trim().Length > InvoicePayment.MaxReasonLength)
            throw new InvoiceRuleException("reason_too_long", $"The reason can be at most {InvoicePayment.MaxReasonLength} characters.");

        var reversal = InvoicePayment.Create(this, -original.AmountPiastres, today, original.Label, actorId, original.Id, reason);
        payments.Add(reversal);
        RowVersion++;
        return reversal;
    }

    /// <summary>Notes that the invoice was emailed to the client. Can happen any number of times.</summary>
    public void MarkEmailed(Guid actorId, string? note = null)
    {
        EnsureNotClosed();
        ClientEmailedAt = DateTimeOffset.UtcNow;
        events.Add(InvoiceEvent.Create(this, InvoiceEventKind.Emailed, actorId, note));
        RowVersion++;
    }

    /// <summary>Hands the invoice to the financial manager. Once.</summary>
    public void HandOver(Guid actorId)
    {
        EnsureNotClosed();
        if (Stage != InvoiceStage.Open)
            throw new InvoiceRuleException("wrong_stage", "The invoice was already handed over.");

        Stage = InvoiceStage.WithFinance;
        HandedOverAt = DateTimeOffset.UtcNow;
        HandedOverBy = actorId;
        events.Add(InvoiceEvent.Create(this, InvoiceEventKind.HandedOver, actorId));
        RowVersion++;
    }

    /// <summary>Closes an invoice that is with finance and has nothing left to pay.</summary>
    public void Close(Guid actorId)
    {
        EnsureNotClosed();
        if (Stage != InvoiceStage.WithFinance)
            throw new InvoiceRuleException("wrong_stage", "Only an invoice that was handed to finance can be closed.");
        if (BalancePiastres != 0)
            throw new InvoiceRuleException("balance_not_zero", "The invoice still has a balance to pay.");

        FinishClose(actorId, null, withBalance: false);
    }

    /// <summary>Closes the invoice whatever is still owed, with a written reason (a job that was never delivered, for example).</summary>
    public void CloseManually(Guid actorId, string reason)
    {
        EnsureNotClosed();
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvoiceRuleException("reason_required", "A reason is required.");
        if (reason.Trim().Length > MaxNoteLength)
            throw new InvoiceRuleException("reason_too_long", $"The reason can be at most {MaxNoteLength} characters.");

        FinishClose(actorId, reason, withBalance: BalancePiastres > 0);
    }

    private void FinishClose(Guid actorId, string? note, bool withBalance)
    {
        Stage = InvoiceStage.Closed;
        ClosedAt = DateTimeOffset.UtcNow;
        ClosedBy = actorId;
        ClosedWithBalance = withBalance;
        events.Add(InvoiceEvent.Create(this, InvoiceEventKind.Closed, actorId, note));
        RowVersion++;
    }

    private void EnsureNotClosed()
    {
        if (Stage == InvoiceStage.Closed)
            throw new InvoiceRuleException("invoice_closed", "The invoice is closed.");
    }
}
