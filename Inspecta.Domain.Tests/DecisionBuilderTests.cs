using Inspecta.Application.Contracts.Executive;
using Inspecta.Application.Executive;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>"Needs your decision": which rows appear, in which order, and how many.</summary>
public sealed class DecisionBuilderTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    private static DateTimeOffset At(int daysAgo) => new(Today.AddDays(-daysAgo).ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(2));

    private static ExecInvoice Invoice(long total, int ageDays, InvoiceStage stage = InvoiceStage.WithFinance, Guid? client = null, string name = "Client", bool closedWithBalance = false) =>
        new(Guid.NewGuid(), "INV-" + Guid.NewGuid().ToString("N")[..4], Company, Guid.NewGuid(), client ?? Guid.NewGuid(), name, total, At(ageDays), stage, closedWithBalance, [], []);

    private static ExecRequest Request(JobStatus status, int receivedDaysAgo = 5, int? inStatusDaysAgo = null, Guid? reviewer = null, Guid? coordinator = null, Guid? client = null) =>
        new(Guid.NewGuid(), Company, client ?? Guid.NewGuid(), "Client", RequestKind.Standard, RequestTriage.Request, status, reviewer, coordinator, null, At(receivedDaysAgo), null, 1, [],
            inStatusDaysAgo is int days ? [new ExecTransition(null, status, At(days), Actor)] : []);

    private static ExecMachine Machine(Guid client, int dueInDays) =>
        new(Guid.NewGuid(), "Crane", null, null, client, null, new DateTimeOffset(Today.AddDays(dueInDays).ToDateTime(new TimeOnly(9, 0)), TimeSpan.FromHours(2)));

    private static IReadOnlyList<Decision> Build(
        IReadOnlyList<ExecInvoice>? invoices = null, IReadOnlyList<ExecRequest>? requests = null, IReadOnlyList<ExecQuote>? quotes = null,
        IReadOnlyList<ExecMachine>? machines = null, IReadOnlyList<ExecClient>? clients = null, IReadOnlyList<ExecPerson>? people = null) =>
        DecisionBuilder.Build(invoices ?? [], requests ?? [], quotes ?? [], machines ?? [], clients ?? [], people ?? [], Today);

    [Fact]
    public void An_invoice_older_than_60_days_with_a_balance_is_a_red_money_row_and_closed_or_paid_ones_are_not()
    {
        var old = Invoice(900, 70);
        var rows = Build(invoices: [old, Invoice(500, 60), Invoice(400, 90, InvoiceStage.Closed, closedWithBalance: true), Invoice(300, 80) with { Payments = [new ExecPayment(Today, 300, At(0), null)] }]);

        var row = Assert.Single(rows);
        Assert.Equal((DecisionKind.Money, DecisionSeverity.Red, "invoice_unpaid_old", 70, 900L, ExecutiveScreen.Money), (row.Kind, row.Severity, row.TitleCode, row.WaitingDays, row.AmountPiastres, row.Screen));
        Assert.Equal(old.Id, row.TargetId);
        Assert.Equal("financial_manager", row.Owner.Role);
    }

    [Fact]
    public void At_most_three_old_invoices_appear_biggest_balance_first()
    {
        var invoices = new[] { Invoice(100, 70), Invoice(900, 70), Invoice(500, 70), Invoice(700, 70) };

        var rows = Build(invoices: invoices);

        Assert.Equal([900L, 700L, 500L], rows.Select(row => row.AmountPiastres!.Value));
    }

    [Fact]
    public void A_client_with_machines_past_their_date_is_a_red_renewals_row_with_the_oldest_overdue_as_waiting()
    {
        var steel = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var clients = new[] { new ExecClient(steel, "Steel", null, false, At(500), null, []) };
        var people = new[] { new ExecPerson(reviewer, "Mona", UserRole.TechnicalManager, true, null) };
        var rows = Build(requests: [Request(JobStatus.InProgress, reviewer: reviewer, client: steel)], machines: [Machine(steel, -5), Machine(steel, -61), Machine(steel, 4)], clients: clients, people: people);

        var row = Assert.Single(rows);
        Assert.Equal((DecisionKind.Renewals, "machines_overdue", "Steel", 61, ExecutiveScreen.Renewals), (row.Kind, row.TitleCode, row.ClientName, row.WaitingDays, row.Screen));
        Assert.Equal("Mona", row.Owner.Name);
        Assert.Equal(steel, row.TargetId);
        Assert.Contains("2 machines", row.Title);
    }

    [Fact]
    public void A_countered_quotation_is_a_violet_row_with_what_the_client_asks_and_a_closed_job_is_not()
    {
        var countered = Request(JobStatus.ClientCountered, inStatusDaysAgo: 2);
        var quote = new ExecQuote(Guid.NewGuid(), countered.Id, 1, QuoteStatus.Open, At(5), null, null, At(-5), false, 486_000, 430_000);

        var rows = Build(requests: [countered, Request(JobStatus.SentToClient)], quotes: [quote]);

        var row = Assert.Single(rows);
        Assert.Equal((DecisionKind.Quotation, DecisionSeverity.Violet, "quotation_countered", 2, 430_000L), (row.Kind, row.Severity, row.TitleCode, row.WaitingDays, row.AmountPiastres));
    }

    [Fact]
    public void A_job_stuck_30_days_is_an_amber_work_row_owned_by_the_reviewer_when_it_is_in_review()
    {
        var reviewer = Guid.NewGuid();
        var coordinator = Guid.NewGuid();
        var people = new[] { new ExecPerson(reviewer, "Hala", UserRole.TechnicalManager, true, null), new ExecPerson(coordinator, "Ahmed", UserRole.Coordinator, true, null) };
        var inReview = Request(JobStatus.CertificateReturned, inStatusDaysAgo: 31, reviewer: reviewer, coordinator: coordinator);
        var inField = Request(JobStatus.InProgress, inStatusDaysAgo: 35, reviewer: reviewer, coordinator: coordinator);
        var fresh = Request(JobStatus.InProgress, inStatusDaysAgo: 29);

        var rows = Build(requests: [inReview, inField, fresh], people: people);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal((DecisionKind.Work, DecisionSeverity.Amber, "job_stuck"), (row.Kind, row.Severity, row.TitleCode)));
        Assert.Equal(["Ahmed", "Hala"], rows.Select(row => row.Owner.Name));
        Assert.Equal([35, 31], rows.Select(row => row.WaitingDays));
    }

    [Fact]
    public void Unanswered_quotations_are_one_amber_row_with_the_count_and_the_amount()
    {
        var one = Request(JobStatus.QuoteSent);
        var two = Request(JobStatus.QuoteSent);
        var quotes = new[]
        {
            new ExecQuote(Guid.NewGuid(), one.Id, 1, QuoteStatus.Open, At(14), null, null, At(-5), false, 1_000, null),
            new ExecQuote(Guid.NewGuid(), two.Id, 1, QuoteStatus.Open, At(20), null, null, At(-5), false, 2_000, null),
            new ExecQuote(Guid.NewGuid(), Guid.NewGuid(), 1, QuoteStatus.Open, At(13), null, null, At(-5), false, 9_000, null)
        };

        var rows = Build(requests: [one, two], quotes: quotes);

        var row = Assert.Single(rows);
        Assert.Equal(("quotations_unanswered", 20, 3_000L), (row.TitleCode, row.WaitingDays, row.AmountPiastres));
        Assert.Contains("2 quotations", row.Title);
    }

    [Fact]
    public void Certificates_waiting_more_than_3_days_are_one_violet_row_per_reviewer()
    {
        var hala = Guid.NewGuid();
        var mona = Guid.NewGuid();
        var people = new[] { new ExecPerson(hala, "Hala", UserRole.TechnicalManager, true, null), new ExecPerson(mona, "Mona", UserRole.TechnicalManager, true, null) };
        var requests = new[]
        {
            Request(JobStatus.CertificateSubmitted, inStatusDaysAgo: 4, reviewer: hala), Request(JobStatus.CertificateSubmitted, inStatusDaysAgo: 6, reviewer: hala),
            Request(JobStatus.CertificateSubmitted, inStatusDaysAgo: 3, reviewer: mona)
        };

        var rows = Build(requests: requests, people: people);

        var row = Assert.Single(rows);
        Assert.Equal(("certificates_waiting", "Hala", 6), (row.TitleCode, row.Owner.Name, row.WaitingDays));
    }

    [Fact]
    public void The_order_is_red_then_violet_then_amber_longest_waiting_first_and_the_list_stops_at_ten()
    {
        var oldInvoices = Enumerable.Range(0, 3).Select(index => Invoice(1_000 + index, 70 + index * 10)).ToList();
        var steel = Guid.NewGuid();
        var counters = Enumerable.Range(0, 4).Select(index => Request(JobStatus.ClientCountered, inStatusDaysAgo: 2 + index)).ToList();
        var stuck = Enumerable.Range(0, 3).Select(index => Request(JobStatus.InProgress, inStatusDaysAgo: 40 + index)).ToList();
        var rows = Build(invoices: oldInvoices, requests: [.. counters, .. stuck], machines: [Machine(steel, -2)]);

        Assert.Equal(10, rows.Count);
        var severities = rows.Select(row => row.Severity).ToList();
        Assert.Equal(severities.OrderBy(severity => severity == DecisionSeverity.Red ? 0 : severity == DecisionSeverity.Violet ? 1 : 2), severities);
        Assert.Equal([90, 80, 70], rows.Where(row => row.Kind == DecisionKind.Money).Select(row => row.WaitingDays));
        Assert.Equal([5, 4, 3, 2], rows.Where(row => row.Kind == DecisionKind.Quotation).Select(row => row.WaitingDays));
    }

    [Fact]
    public void Nothing_to_decide_is_an_empty_list() => Assert.Empty(Build());
}
