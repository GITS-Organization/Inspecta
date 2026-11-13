using Inspecta.Application.Contracts.Executive;
using Inspecta.Application.Executive;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>Machines due and client health, one definition at a time, over hand-made rows.</summary>
public sealed class RenewalsAndClientFiguresTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);
    private static readonly Guid Company = Guid.NewGuid();

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(new TimeOnly(9, 0)), TimeSpan.FromHours(2));

    private static ExecMachine Machine(int dueInDays, Guid? clientId = null, CertificateResult? result = CertificateResult.SafeToOperate, string name = "Crane") =>
        new(Guid.NewGuid(), name, "S-" + Guid.NewGuid().ToString("N")[..4], "5 t", clientId ?? Guid.NewGuid(), result, At(Today.AddDays(dueInDays)));

    private static ExecClient Client(Guid id, string name = "Client", string? location = "Cairo", bool contract = false, DateTimeOffset? archived = null) =>
        new(id, name, location, contract, At(new DateOnly(2021, 3, 4)), archived, []);

    private static ExecRequest Request(Guid? clientId, JobStatus status = JobStatus.InProgress, RequestTriage triage = RequestTriage.Request, Guid? reviewer = null, Guid[]? equipment = null, int receivedDaysAgo = 5) =>
        new(Guid.NewGuid(), Company, clientId, "Client", RequestKind.Standard, triage, status, reviewer, null, null, At(Today.AddDays(-receivedDaysAgo)), null, 1, equipment ?? [], []);

    private static ExecInvoice Invoice(Guid clientId, long total, int ageDays, long paid = 0, InvoiceStage stage = InvoiceStage.WithFinance) =>
        new(Guid.NewGuid(), "INV-1", Company, Guid.NewGuid(), clientId, "Client", total, At(Today.AddDays(-ageDays)), stage, false,
            paid > 0 ? [new ExecPayment(Today.AddDays(-1), paid, At(Today), null)] : [], []);

    private static ExecutiveData Data(IReadOnlyList<ExecClient> clients, IReadOnlyList<ExecMachine> machines, IReadOnlyList<ExecPerson>? people = null) =>
        new([], [], [], [], machines, clients, people ?? []);

    [Fact]
    public void Buckets_are_cumulative_from_today_and_overdue_stands_apart()
    {
        var machines = new[] { Machine(-30), Machine(-1), Machine(0), Machine(7), Machine(8), Machine(30), Machine(31), Machine(60), Machine(61), Machine(90), Machine(91) };

        var buckets = RenewalsFigures.Buckets(machines, Today);

        Assert.Equal(new RenewalBuckets(2, 2, 4, 6, 8), buckets);
    }

    [Fact]
    public void The_weekly_strip_is_overdue_then_eleven_weeks_starting_today()
    {
        var machines = new[] { Machine(-5), Machine(-1), Machine(0), Machine(6), Machine(7), Machine(70), Machine(76), Machine(77) };

        var weekly = RenewalsFigures.Weekly(machines, Today);

        Assert.Equal(12, weekly.Count);
        Assert.Equal([2, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 2], weekly);
    }

    [Fact]
    public void A_machine_has_a_job_when_an_open_request_has_an_item_for_it()
    {
        var withJob = Machine(5);
        var noJob = Machine(6);
        var closedJob = Machine(7);
        var maybeJob = Machine(8);
        var farAway = Machine(40);
        var requests = new[]
        {
            Request(null, equipment: [withJob.Id, farAway.Id]),
            Request(null, status: JobStatus.SentToClient, equipment: [closedJob.Id]),
            Request(null, triage: RequestTriage.Maybe, equipment: [maybeJob.Id])
        };
        var machines = new[] { withJob, noJob, closedJob, maybeJob, farAway };

        Assert.Equal(1, RenewalsFigures.WithJob(machines, requests, Today));
        var items = RenewalsFigures.Items(machines, [], requests, "all", Today);
        Assert.True(items.Single(item => item.EquipmentId == withJob.Id).JobOpened);
        Assert.False(items.Single(item => item.EquipmentId == closedJob.Id).JobOpened);
    }

    [Fact]
    public void Safe_percent_counts_only_machines_that_have_a_result()
    {
        var machines = new[] { Machine(1), Machine(1), Machine(1, result: CertificateResult.NotSafe), Machine(1, result: null) };

        Assert.Equal(67, RenewalsFigures.SafePercent(machines));
        Assert.Equal(0, RenewalsFigures.SafePercent([Machine(1, result: null)]));
    }

    [Theory]
    [InlineData("all", 5)]
    [InlineData("overdue", 1)]
    [InlineData("soon", 2)]
    [InlineData("later", 2)]
    [InlineData("notSafe", 2)]
    public void The_filters_keep_the_machines_they_name_ordered_by_due_date(string filter, int expected)
    {
        var machines = new[]
        {
            Machine(-3, result: CertificateResult.SafeWithConditions), Machine(0), Machine(30, result: CertificateResult.NotSafe), Machine(31), Machine(90)
        };

        var items = RenewalsFigures.Items(machines, [], [], filter, Today);

        Assert.Equal(expected, items.Count);
        Assert.Equal(items.OrderBy(item => item.NextDueAt).Select(item => item.EquipmentId), items.Select(item => item.EquipmentId));
        Assert.Equal(-3, RenewalsFigures.Items(machines, [], [], "all", Today)[0].DaysUntilDue);
    }

    [Fact]
    public void An_unknown_filter_is_refused() =>
        Assert.Throws<ArgumentException>(() => RenewalsFigures.Items([], [], [], "sideways", Today));

    [Fact]
    public void Most_overdue_by_client_is_ordered_and_names_the_client()
    {
        var steel = Guid.NewGuid();
        var cement = Guid.NewGuid();
        var machines = new[] { Machine(-2, steel), Machine(-9, steel), Machine(-1, cement), Machine(5, cement) };

        var list = RenewalsFigures.MostOverdueByClient(machines, [Client(steel, "Steel"), Client(cement, "Cement")], Today);

        Assert.Equal([("Steel", 2), ("Cement", 1)], list.Select(item => (item.ClientName, item.Overdue)));
    }

    [Fact]
    public void Client_health_is_healthy_watch_or_at_risk_by_the_rule()
    {
        var healthy = Guid.NewGuid();
        var lateMachine = Guid.NewGuid();
        var warmInvoice = Guid.NewGuid();
        var oldInvoice = Guid.NewGuid();
        var both = Guid.NewGuid();
        var clients = new[] { Client(healthy, "A"), Client(lateMachine, "B"), Client(warmInvoice, "C"), Client(oldInvoice, "D"), Client(both, "E") };
        var data = Data(clients, [Machine(5, healthy), Machine(-3, lateMachine), Machine(-3, both)]);
        var invoices = new[] { Invoice(warmInvoice, 1_000, 45), Invoice(oldInvoice, 1_000, 70), Invoice(both, 1_000, 70), Invoice(healthy, 1_000, 70, paid: 1_000) };

        var rows = ClientFigures.Rows(data, [], invoices, Today).ToDictionary(row => row.Name);

        Assert.Equal(ClientHealth.Healthy, rows["A"].Health);
        Assert.Equal(ClientHealth.Watch, rows["B"].Health);
        Assert.Equal(ClientHealth.Watch, rows["C"].Health);
        Assert.Equal(ClientHealth.Watch, rows["D"].Health);
        Assert.Equal(ClientHealth.AtRisk, rows["E"].Health);
        Assert.Equal(1_000, rows["E"].OlderThan60Piastres);
        Assert.Equal(1, rows["E"].OverdueMachines);
        Assert.Equal(-3, rows["E"].NextDueInDays);
        Assert.Equal(5, rows["A"].NextDueInDays);
    }

    [Fact]
    public void An_invoice_closed_by_hand_does_not_make_a_client_unhealthy()
    {
        var id = Guid.NewGuid();
        var closed = Invoice(id, 1_000, 90, stage: InvoiceStage.Closed) with { ClosedWithBalance = true };

        var row = ClientFigures.Rows(Data([Client(id)], []), [], [closed], Today).Single();

        Assert.Equal(ClientHealth.Healthy, row.Health);
        Assert.Equal(0, row.OwedPiastres);
    }

    [Fact]
    public void Rows_leave_out_archived_clients_count_open_jobs_and_name_the_latest_reviewer()
    {
        var id = Guid.NewGuid();
        var archived = Guid.NewGuid();
        var reviewerOld = Guid.NewGuid();
        var reviewerNew = Guid.NewGuid();
        var people = new[] { new ExecPerson(reviewerOld, "Old Reviewer", UserRole.TechnicalManager, true, null), new ExecPerson(reviewerNew, "New Reviewer", UserRole.TechnicalManager, true, null) };
        var requests = new[]
        {
            Request(id, reviewer: reviewerOld, receivedDaysAgo: 40, status: JobStatus.SentToClient),
            Request(id, reviewer: reviewerNew, receivedDaysAgo: 3),
            Request(id, triage: RequestTriage.Maybe, receivedDaysAgo: 1)
        };
        var data = Data([Client(id, "Active Co", "Suez", contract: true), Client(archived, "Gone", archived: At(Today))], [], people);

        var rows = ClientFigures.Rows(data, requests, [], Today);

        var row = Assert.Single(rows);
        Assert.Equal((id, "Suez", true, 2021, 1, "New Reviewer"), (row.Id, row.City, row.Contract, row.Since, row.OpenJobs, row.ReviewerName));
    }

    [Fact]
    public void Invoiced_year_to_date_counts_invoices_created_since_the_first_of_january_and_the_order_is_biggest_first()
    {
        var small = Guid.NewGuid();
        var big = Guid.NewGuid();
        var invoices = new[] { Invoice(small, 100, 5), Invoice(big, 900, 10), Invoice(big, 5_000, 365) };

        var rows = ClientFigures.Rows(Data([Client(small, "Small"), Client(big, "Big")], []), [], invoices, Today);

        Assert.Equal(["Big", "Small"], rows.Select(row => row.Name));
        Assert.Equal(900, rows[0].InvoicedYtdPiastres);
    }

    [Fact]
    public void Counts_ignore_the_filters_and_the_filters_narrow_by_health_and_text()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var data = Data([Client(a, "Alpha Steel", "Cairo", contract: true), Client(b, "Beta Cement", "Suez")], [Machine(-2, b)]);
        var rows = ClientFigures.Rows(data, [], [], Today);

        var counts = ClientFigures.Counts(rows);

        Assert.Equal(new ClientCounts(2, 1, 1, 1, 0), counts);
        Assert.Equal(["Beta Cement"], ClientFigures.Filter(rows, ClientHealth.Watch, null).Select(row => row.Name));
        Assert.Equal(["Beta Cement"], ClientFigures.Filter(rows, null, "suez").Select(row => row.Name));
        Assert.Equal(2, ClientFigures.Filter(rows, null, "  ").Count);
        Assert.Equal(ClientHealth.AtRisk, ClientFigures.ParseHealth("at_risk"));
        Assert.Throws<ArgumentException>(() => ClientFigures.ParseHealth("fine"));
    }
}
