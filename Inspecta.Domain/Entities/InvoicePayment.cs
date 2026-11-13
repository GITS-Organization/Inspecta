namespace Inspecta.Domain.Entities;

/// <summary>
/// Money the company received on an invoice. Only ever added: a mistake is fixed by a reversing row (a negative amount that points at the
/// payment it undoes), so the history keeps both.
/// </summary>
public sealed class InvoicePayment
{
    public const int MaxLabelLength = 120;
    public const int MaxReasonLength = 500;

    private InvoicePayment() { }

    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Guid ManagerId { get; private set; }

    /// <summary>Piastres: positive for a payment, negative for a reversal.</summary>
    public long AmountPiastres { get; private set; }

    /// <summary>The day the money was received. A reversal carries the day it was recorded.</summary>
    public DateOnly PaidOn { get; private set; }

    /// <summary>Free text such as "Advance" or "After the visit".</summary>
    public string? Label { get; private set; }

    public Guid? ReversesPaymentId { get; private set; }
    public string? Reason { get; private set; }
    public Guid RecordedBy { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public bool IsReversal => ReversesPaymentId is not null;

    internal static InvoicePayment Create(
        Invoice invoice, long amountPiastres, DateOnly paidOn, string? label, Guid recordedBy, Guid? reverses = null, string? reason = null) => new()
    {
        Id = Guid.NewGuid(),
        InvoiceId = invoice.Id,
        ManagerId = invoice.ManagerId,
        AmountPiastres = amountPiastres,
        PaidOn = paidOn,
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
        ReversesPaymentId = reverses,
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        RecordedBy = recordedBy,
        RecordedAt = DateTimeOffset.UtcNow
    };
}
