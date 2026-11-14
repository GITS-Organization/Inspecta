using Inspecta.Application.Contracts.Executive;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Executive;

/// <summary>What the financial manager has to do next and what was just received. Pure, over the invoices of the company.</summary>
public static class FinanceFigures
{
    /// <summary>Handed to finance, paid in full and not closed: the oldest few are listed, all are counted.</summary>
    public static ReadyToClose ReadyToClose(IEnumerable<ExecInvoice> invoices, DateOnly today, int take)
    {
        var ready = invoices.Where(invoice => invoice.Stage == InvoiceStage.WithFinance && invoice.TotalPiastres > 0 && invoice.BalancePiastres == 0).ToList();
        return new ReadyToClose(
            ready.Count, ready.Sum(invoice => invoice.TotalPiastres),
            ready.OrderByDescending(invoice => MoneyFigures.AgeDays(invoice, today)).ThenBy(invoice => invoice.Number, StringComparer.Ordinal).Take(take)
                .Select(invoice => new ReadyToCloseItem(invoice.Id, invoice.Number, invoice.ClientName.Trim(), invoice.TotalPiastres, MoneyFigures.AgeDays(invoice, today))).ToList());
    }

    /// <summary>Invoices still with the coordinator that owe something, and what they owe.</summary>
    public static NotHandedOver NotHandedOver(IEnumerable<ExecInvoice> invoices)
    {
        var waiting = invoices.Where(invoice => invoice.Stage == InvoiceStage.Open && invoice.BalancePiastres > 0).ToList();
        return new NotHandedOver(waiting.Count, waiting.Sum(invoice => invoice.BalancePiastres));
    }

    /// <summary>Invoices with finance, older than 60 days and still owing, the oldest first.</summary>
    public static IReadOnlyList<OldUnpaidItem> OldUnpaid(IEnumerable<ExecInvoice> invoices, DateOnly today, int take) =>
        invoices.Where(invoice => invoice.Stage == InvoiceStage.WithFinance && invoice.BalancePiastres > 0 && MoneyFigures.AgeDays(invoice, today) > 60)
            .OrderByDescending(invoice => MoneyFigures.AgeDays(invoice, today)).ThenBy(invoice => invoice.Number, StringComparer.Ordinal).Take(take)
            .Select(invoice => new OldUnpaidItem(invoice.Id, invoice.Number, invoice.ClientName.Trim(), invoice.BalancePiastres, MoneyFigures.AgeDays(invoice, today))).ToList();

    /// <summary>The money recorded most recently (reversals left out), newest first, with who recorded it.</summary>
    public static IReadOnlyList<LatestPayment> LatestPayments(IEnumerable<ExecInvoice> invoices, IEnumerable<ExecPerson> people, int take)
    {
        var names = people.ToDictionary(person => person.Id, person => person.Name);
        return invoices
            .SelectMany(invoice => invoice.Payments.Where(payment => payment.AmountPiastres > 0 && payment.ReversesPaymentId is null).Select(payment => (invoice, payment)))
            .OrderByDescending(item => item.payment.RecordedAt)
            .Take(take)
            .Select(item => new LatestPayment(
                item.payment.RecordedAt, item.payment.PaidOn, item.invoice.Id, item.invoice.Number, item.invoice.ClientName.Trim(), item.payment.AmountPiastres,
                item.payment.Label, item.payment.RecordedBy is Guid by ? names.GetValueOrDefault(by) : null)).ToList();
    }
}
