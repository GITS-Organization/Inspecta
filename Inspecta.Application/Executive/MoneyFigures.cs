using Inspecta.Application.Contracts.Executive;
using Inspecta.Application.Services;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Executive;

/// <summary>
/// The money numbers of the executive screens, each one as the spec defines it. Pure: rows in, figures out. Days are Cairo days; an invoice
/// closed by hand is out of everything that is "still owed" and is shown only as its own figure.
/// </summary>
public static class MoneyFigures
{
    /// <summary>Invoices created in the period, by their total.</summary>
    public static long Invoiced(IEnumerable<ExecInvoice> invoices, DateOnly from, DateOnly to) =>
        invoices.Where(invoice => InRange(InvoiceCalendar.Of(invoice.CreatedAt), from, to)).Sum(invoice => invoice.TotalPiastres);

    /// <summary>Payments and reversals dated in the period (a reversal is negative money on the day it was recorded).</summary>
    public static long Collected(IEnumerable<ExecInvoice> invoices, DateOnly from, DateOnly to) =>
        invoices.SelectMany(invoice => invoice.Payments).Where(payment => InRange(payment.PaidOn, from, to)).Sum(payment => payment.AmountPiastres);

    public static int RatePercent(long collectedPiastres, long invoicedPiastres) =>
        invoicedPiastres <= 0 ? 0 : (int)Math.Round(collectedPiastres * 100m / invoicedPiastres, MidpointRounding.AwayFromZero);

    public static OutstandingKpi Outstanding(IEnumerable<ExecInvoice> invoices, DateOnly today)
    {
        var list = invoices.ToList();
        var open = StillOwed(list).ToList();
        var old = open.Where(invoice => AgeDays(invoice, today) > 60).ToList();
        var closedByHand = list.Where(invoice => invoice.Stage == InvoiceStage.Closed && invoice.BalancePiastres > 0).Sum(invoice => invoice.BalancePiastres);
        return new OutstandingKpi(open.Sum(invoice => invoice.BalancePiastres), old.Sum(invoice => invoice.BalancePiastres), old.Count, closedByHand);
    }

    public static IReadOnlyList<AgingBucket> Aging(IEnumerable<ExecInvoice> invoices, DateOnly today)
    {
        var open = StillOwed(invoices).ToList();
        AgingBucket Bucket(string name, Func<int, bool> inside)
        {
            var inBucket = open.Where(invoice => inside(AgeDays(invoice, today))).ToList();
            return new AgingBucket(name, inBucket.Sum(invoice => invoice.BalancePiastres), inBucket.Count);
        }

        return [Bucket("0_30", age => age <= 30), Bucket("31_60", age => age is > 30 and <= 60), Bucket("61_90", age => age is > 60 and <= 90), Bucket("over_90", age => age > 90)];
    }

    /// <summary>
    /// Invoices whose balance is zero today and reached zero in the period: the average days from creation to the payment that brought it to zero
    /// (payments in the order of their day, then of when they were recorded). An invoice that was reversed and owes again is not counted.
    /// </summary>
    public static int DaysToCollect(IEnumerable<ExecInvoice> invoices, DateOnly from, DateOnly to)
    {
        var days = new List<int>();
        foreach (var invoice in invoices)
        {
            if (invoice.TotalPiastres <= 0 || invoice.BalancePiastres != 0) continue;
            // The payment that finally brought the balance to zero: the last one after which the running total went from below the total to the total.
            long running = 0;
            ExecPayment? closing = null;
            foreach (var payment in invoice.Payments.OrderBy(item => item.PaidOn).ThenBy(item => item.RecordedAt))
            {
                var before = running;
                running += payment.AmountPiastres;
                if (before < invoice.TotalPiastres && running >= invoice.TotalPiastres) closing = payment;
            }

            if (closing is not null && InRange(closing.PaidOn, from, to))
                days.Add(Math.Max(0, closing.PaidOn.DayNumber - InvoiceCalendar.Of(invoice.CreatedAt).DayNumber));
        }

        return days.Count == 0 ? 0 : (int)Math.Round(days.Average(), MidpointRounding.AwayFromZero);
    }

    /// <summary>The clients with the biggest unpaid balances (by client id, else by the name on the invoice), with the part older than 60 days.</summary>
    public static IReadOnlyList<OwedByClient> WhoOwesMost(IEnumerable<ExecInvoice> invoices, DateOnly today, int take) =>
        StillOwed(invoices)
            .GroupBy(invoice => ClientKey(invoice))
            .Select(group => new OwedByClient(
                group.First().ClientId, group.First().ClientName.Trim(),
                group.Sum(invoice => invoice.BalancePiastres),
                group.Where(invoice => AgeDays(invoice, today) > 60).Sum(invoice => invoice.BalancePiastres)))
            .OrderByDescending(item => item.OwedPiastres).ThenBy(item => item.ClientName, StringComparer.OrdinalIgnoreCase)
            .Take(take).ToList();

    /// <summary>The last twelve calendar months ending with the month of <paramref name="today"/>: invoiced by creation month, collected by payment month.</summary>
    public static IReadOnlyList<MonthlyMoney> Monthly(IReadOnlyCollection<ExecInvoice> invoices, DateOnly today)
    {
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
        var months = Enumerable.Range(0, 12).Select(index => first.AddMonths(index)).ToList();
        return months.Select(month => new MonthlyMoney(
            $"{month.Year:D4}-{month.Month:D2}",
            invoices.Where(invoice => SameMonth(InvoiceCalendar.Of(invoice.CreatedAt), month)).Sum(invoice => invoice.TotalPiastres),
            invoices.SelectMany(invoice => invoice.Payments).Where(payment => SameMonth(payment.PaidOn, month)).Sum(payment => payment.AmountPiastres))).ToList();
    }

    /// <summary>The key a client is grouped by: its id, or the name on the invoice (case and spaces ignored) when there is none.</summary>
    internal static string ClientKey(ExecInvoice invoice) =>
        invoice.ClientId is Guid id ? id.ToString() : "name:" + invoice.ClientName.Trim().ToLowerInvariant();

    internal static int AgeDays(ExecInvoice invoice, DateOnly today) => today.DayNumber - InvoiceCalendar.Of(invoice.CreatedAt).DayNumber;

    /// <summary>Invoices that are not closed and still have a balance.</summary>
    internal static IEnumerable<ExecInvoice> StillOwed(IEnumerable<ExecInvoice> invoices) =>
        invoices.Where(invoice => invoice.Stage != InvoiceStage.Closed && invoice.BalancePiastres > 0);

    private static bool InRange(DateOnly day, DateOnly from, DateOnly to) => day >= from && day <= to;

    private static bool SameMonth(DateOnly day, DateOnly month) => day.Year == month.Year && day.Month == month.Month;
}
