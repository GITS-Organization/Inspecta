using Inspecta.Application.Executive;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The money figures of the executive screens, one definition at a time, over hand-made rows.</summary>
public sealed class MoneyFiguresTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);
    private static readonly Guid Company = Guid.NewGuid();

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(2));

    private static ExecInvoice Invoice(
        long total, int ageDays, InvoiceStage stage = InvoiceStage.Open, Guid? clientId = null, string client = "Client", bool closedWithBalance = false,
        params (int DaysAgo, long Amount)[] payments)
    {
        var created = Today.AddDays(-ageDays);
        return new ExecInvoice(
            Guid.NewGuid(), "INV-" + Guid.NewGuid().ToString("N")[..6], Company, Guid.NewGuid(), clientId, client, total, At(created), stage, closedWithBalance,
            payments.Select((payment, index) => new ExecPayment(Today.AddDays(-payment.DaysAgo), payment.Amount, At(Today.AddDays(-payment.DaysAgo)).AddMinutes(index), null)).ToList(), []);
    }

    private static ExecutivePeriod Month => ExecutivePeriod.Resolve(null, null, Today);

    [Fact]
    public void Invoiced_is_the_total_of_invoices_created_in_the_period_and_collected_is_the_payments_dated_in_it()
    {
        var invoices = new[]
        {
            Invoice(1_000_000, ageDays: 5, payments: [(3, 400_000)]),
            Invoice(500_000, ageDays: 40, payments: [(20, 500_000), (2, 100_000)])
        };

        Assert.Equal(1_000_000, MoneyFigures.Invoiced(invoices, Month.From, Month.To));
        Assert.Equal(500_000, MoneyFigures.Collected(invoices, Month.From, Month.To));
        Assert.Equal(500_000, MoneyFigures.Invoiced(invoices, Today.AddDays(-45), Today.AddDays(-30)));
    }

    [Fact]
    public void A_reversal_counts_as_negative_money_on_its_own_day()
    {
        var invoice = Invoice(1_000_000, ageDays: 30, payments: [(25, 300_000), (1, -300_000)]);

        Assert.Equal(300_000, MoneyFigures.Collected([invoice], Today.AddDays(-30), Today.AddDays(-10)));
        Assert.Equal(-300_000, MoneyFigures.Collected([invoice], Month.From, Month.To));
    }

    [Theory]
    [InlineData(900_000, 1_500_000, 60)]
    [InlineData(0, 0, 0)]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(1_200, 1_000, 120)]
    public void The_rate_is_collected_over_invoiced_in_whole_percent_and_may_pass_100(long collected, long invoiced, int expected) =>
        Assert.Equal(expected, MoneyFigures.RatePercent(collected, invoiced));

    [Fact]
    public void Outstanding_is_the_balance_of_invoices_that_are_not_closed_and_a_closed_by_hand_balance_stands_apart()
    {
        var invoices = new[]
        {
            Invoice(1_000_000, 10, payments: [(5, 400_000)]),
            Invoice(300_000, 70, InvoiceStage.WithFinance),
            Invoice(200_000, 100, InvoiceStage.Closed, closedWithBalance: true, payments: [(90, 50_000)]),
            Invoice(100_000, 20, InvoiceStage.Closed, payments: [(10, 100_000)])
        };

        var outstanding = MoneyFigures.Outstanding(invoices, Today);

        Assert.Equal(900_000, outstanding.TotalPiastres);
        Assert.Equal(300_000, outstanding.OlderThan60Piastres);
        Assert.Equal(1, outstanding.OlderThan60Count);
        Assert.Equal(150_000, outstanding.ClosedByHandPiastres);
    }

    [Fact]
    public void Aging_buckets_follow_the_age_of_the_invoice_and_skip_paid_and_closed_ones()
    {
        var invoices = new[]
        {
            Invoice(100, 0), Invoice(200, 30), Invoice(300, 31), Invoice(400, 60), Invoice(500, 61), Invoice(600, 90), Invoice(700, 91),
            Invoice(800, 50, payments: [(1, 800)]), Invoice(900, 10, InvoiceStage.Closed, closedWithBalance: true)
        };

        var aging = MoneyFigures.Aging(invoices, Today);

        Assert.Equal(["0_30", "31_60", "61_90", "over_90"], aging.Select(item => item.Bucket));
        Assert.Equal([300L, 700L, 1_100L, 700L], aging.Select(item => item.AmountPiastres));
        Assert.Equal([2, 2, 2, 1], aging.Select(item => item.Count));
    }

    [Fact]
    public void Days_to_collect_averages_creation_to_the_payment_that_brought_the_balance_to_zero()
    {
        var invoices = new[]
        {
            Invoice(1_000, 30, payments: [(25, 400), (10, 600)]),  // zero 20 days after creation
            Invoice(1_000, 12, payments: [(6, 1_000)]),             // 6 days
            Invoice(1_000, 40, payments: [(35, 1_000), (3, -1_000)]), // reversed later: not counted
            Invoice(1_000, 50, payments: [(45, 400)]),              // still owed: not counted
            Invoice(1_000, 100, payments: [(95, 1_000)])            // paid before the period
        };

        var days = MoneyFigures.DaysToCollect(invoices, Month.From, Month.To);

        Assert.Equal(13, days);
    }

    [Fact]
    public void Days_to_collect_is_zero_when_nothing_was_fully_paid_in_the_period() =>
        Assert.Equal(0, MoneyFigures.DaysToCollect([Invoice(1_000, 5, payments: [(2, 200)])], Month.From, Month.To));

    [Fact]
    public void Who_owes_the_most_groups_by_client_id_else_by_name_and_leaves_out_closed_invoices()
    {
        var steel = Guid.NewGuid();
        var invoices = new[]
        {
            Invoice(500, 80, clientId: steel, client: "Acme Steel"),
            Invoice(300, 10, clientId: steel, client: "Acme Steel Ltd"),
            Invoice(400, 70, client: "Walk-in"),
            Invoice(100, 5, client: "walk-in "),
            Invoice(900, 5, InvoiceStage.Closed, clientId: Guid.NewGuid(), client: "Closed", closedWithBalance: true)
        };

        var owed = MoneyFigures.WhoOwesMost(invoices, Today, 6);

        Assert.Equal(2, owed.Count);
        Assert.Equal((steel, 800L, 500L), (owed[0].ClientId, owed[0].OwedPiastres, owed[0].OlderThan60Piastres));
        Assert.Equal((null, 500L, 400L), (owed[1].ClientId, owed[1].OwedPiastres, owed[1].OlderThan60Piastres));
        Assert.Equal("Walk-in", owed[1].ClientName);
    }

    [Fact]
    public void Monthly_gives_the_last_twelve_months_with_zeros_for_quiet_ones()
    {
        var invoices = new[]
        {
            Invoice(1_000, 3, payments: [(2, 400)]),
            Invoice(2_000, 45, payments: [(40, 2_000)])
        };

        var months = MoneyFigures.Monthly(invoices, Today);

        Assert.Equal(12, months.Count);
        Assert.Equal("2025-12", months[0].Month);
        Assert.Equal("2026-11", months[11].Month);
        Assert.Equal((1_000L, 400L), (months[11].InvoicedPiastres, months[11].CollectedPiastres));
        Assert.Equal(2_000L, months[9].InvoicedPiastres);
        Assert.Equal(2_000L, months[10].CollectedPiastres);
        Assert.Equal((0L, 0L), (months[0].InvoicedPiastres, months[0].CollectedPiastres));
    }

    [Fact]
    public void Days_to_collect_uses_the_payment_that_finally_brought_the_balance_to_zero()
    {
        var invoice = Invoice(1_000, 30, payments: [(25, 1_000), (20, -1_000), (10, 1_000)]);

        Assert.Equal(20, MoneyFigures.DaysToCollect([invoice], Month.From, Month.To));
        Assert.Equal(0, MoneyFigures.DaysToCollect([invoice], Today.AddDays(-30), Today.AddDays(-22)));
    }
}
