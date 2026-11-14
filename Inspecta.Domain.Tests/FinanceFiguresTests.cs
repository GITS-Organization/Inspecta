using Inspecta.Application.Executive;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>What the financial manager has to do and has just received, one definition at a time over hand-made rows.</summary>
public sealed class FinanceFiguresTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Nadia = Guid.NewGuid();

    private static DateTimeOffset At(int daysAgo, int hour = 10) => new(Today.AddDays(-daysAgo).ToDateTime(new TimeOnly(hour, 0)), TimeSpan.FromHours(2));

    private static ExecInvoice Invoice(string number, long total, int ageDays, InvoiceStage stage = InvoiceStage.WithFinance, string client = "Client", params (int DaysAgo, long Amount, string? Label, bool Reversal)[] payments) =>
        new(Guid.NewGuid(), number, Company, Guid.NewGuid(), Guid.NewGuid(), client, total, At(ageDays), stage, false,
            payments.Select((payment, index) => new ExecPayment(Today.AddDays(-payment.DaysAgo), payment.Amount, At(payment.DaysAgo, 9 + index), payment.Reversal ? Guid.NewGuid() : null, Nadia, payment.Label)).ToList(), []);

    [Fact]
    public void Ready_to_close_is_with_finance_paid_in_full_and_not_closed()
    {
        var invoices = new[]
        {
            Invoice("A", 500, 20, payments: [(3, 500, null, false)]),
            Invoice("B", 300, 30, payments: [(3, 300, null, false)]),
            Invoice("C", 400, 10, payments: [(3, 100, null, false)]),
            Invoice("D", 400, 10, InvoiceStage.Open, payments: [(3, 400, null, false)]),
            Invoice("E", 400, 10, InvoiceStage.Closed, payments: [(3, 400, null, false)])
        };

        var ready = FinanceFigures.ReadyToClose(invoices, Today, 5);

        Assert.Equal(2, ready.Count);
        Assert.Equal(800, ready.TotalPiastres);
        Assert.Equal(["B", "A"], ready.Items.Select(item => item.Number));
        Assert.Equal(30, ready.Items[0].AgeDays);
    }

    [Fact]
    public void Ready_to_close_lists_at_most_the_asked_number_but_counts_all()
    {
        var invoices = Enumerable.Range(0, 7).Select(index => Invoice("R" + index, 100, 10 + index, payments: [(1, 100, null, false)])).ToList();

        var ready = FinanceFigures.ReadyToClose(invoices, Today, 5);

        Assert.Equal(7, ready.Count);
        Assert.Equal(5, ready.Items.Count);
    }

    [Fact]
    public void Not_handed_over_is_the_unpaid_balance_of_invoices_still_with_the_coordinator()
    {
        var invoices = new[]
        {
            Invoice("A", 110, 2, InvoiceStage.Open), Invoice("B", 95, 3, InvoiceStage.Open, payments: [(1, 30, null, false)]),
            Invoice("C", 50, 3, InvoiceStage.Open, payments: [(1, 50, null, false)]), Invoice("D", 999, 3)
        };

        var waiting = FinanceFigures.NotHandedOver(invoices);

        Assert.Equal((2, 175L), (waiting.Count, waiting.BalancePiastres));
    }

    [Fact]
    public void Old_unpaid_is_with_finance_older_than_60_days_with_a_balance_oldest_first_and_at_most_three()
    {
        var invoices = new[]
        {
            Invoice("A", 100, 61), Invoice("B", 200, 96), Invoice("C", 300, 71, payments: [(1, 100, null, false)]), Invoice("D", 400, 90), Invoice("E", 500, 60),
            Invoice("F", 600, 80, InvoiceStage.Open), Invoice("G", 700, 100, payments: [(1, 700, null, false)])
        };

        var old = FinanceFigures.OldUnpaid(invoices, Today, 3);

        Assert.Equal(["B", "D", "C"], old.Select(item => item.Number));
        Assert.Equal(200, old[2].BalancePiastres);
        Assert.Equal(96, old[0].AgeDays);
    }

    [Fact]
    public void Latest_payments_are_real_payments_newest_first_with_who_recorded_them_and_reversals_are_left_out()
    {
        var invoices = new[]
        {
            Invoice("A", 1_000, 30, client: "Nile", payments: [(5, 200, "Advance", false), (2, 300, null, false), (1, -300, null, true)]),
            Invoice("B", 500, 10, client: "Delta", payments: [(0, 100, "After the visit", false)])
        };
        var people = new[] { new ExecPerson(Nadia, "Nadia Samir", UserRole.FinancialManager, true, null) };

        var latest = FinanceFigures.LatestPayments(invoices, people, 2);

        Assert.Equal([("Delta", 100L), ("Nile", 300L)], latest.Select(item => (item.ClientName, item.AmountPiastres)));
        Assert.Equal(("B", "After the visit", "Nadia Samir"), (latest[0].InvoiceNumber, latest[0].Label, latest[0].RecordedByName));
        Assert.Null(latest[1].Label);
    }

    [Fact]
    public void An_empty_company_has_nothing_to_do()
    {
        Assert.Equal(0, FinanceFigures.ReadyToClose([], Today, 5).Count);
        Assert.Equal(0, FinanceFigures.NotHandedOver([]).Count);
        Assert.Empty(FinanceFigures.OldUnpaid([], Today, 3));
        Assert.Empty(FinanceFigures.LatestPayments([], [], 10));
    }
}
