using Inspecta.Application.Contracts.Executive;
using Inspecta.Application.Executive;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The company feed and the next seven days.</summary>
public sealed class ActivityBuilderTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Mona = Guid.NewGuid();

    private static DateTimeOffset At(int daysAgo, int hour = 10) => new(Today.AddDays(-daysAgo).ToDateTime(new TimeOnly(hour, 0)), TimeSpan.FromHours(2));

    private static ExecRequest Request(string client, JobStatus status, RequestTriage triage = RequestTriage.Request, DateTimeOffset? scheduled = null, Guid? inspector = null, params (JobStatus To, int DaysAgo, int Hour)[] history) =>
        new(Guid.NewGuid(), Company, Guid.NewGuid(), client, RequestKind.Standard, triage, status, null, null, inspector, At(30), scheduled, 3, [],
            history.Select(entry => new ExecTransition(null, entry.To, At(entry.DaysAgo, entry.Hour), Mona)).ToList());

    private static ExecutiveData Data(IReadOnlyList<ExecRequest>? requests = null, IReadOnlyList<ExecInvoice>? invoices = null, IReadOnlyList<ExecCertificate>? certificates = null,
        IReadOnlyList<ExecClient>? clients = null, IReadOnlyList<ExecQuote>? quotes = null) =>
        new(invoices ?? [], requests ?? [], quotes ?? [], certificates ?? [], [], clients ?? [], [new ExecPerson(Mona, "Mona Adel", UserRole.TechnicalManager, true, null)]);

    [Fact]
    public void Status_changes_become_coded_events_with_the_actor_and_the_request_number_and_edits_and_maybe_emails_do_not()
    {
        var request = Request("Delta Steel", JobStatus.SentToClient, history:
        [
            (JobStatus.QuoteDraft, 20, 9), (JobStatus.QuoteSent, 19, 9), (JobStatus.Unassigned, 15, 9), (JobStatus.Assigned, 14, 9), (JobStatus.TaskAccepted, 13, 9),
            (JobStatus.InProgress, 12, 9), (JobStatus.CertificateSubmitted, 5, 9), (JobStatus.CertificateApproved, 3, 9), (JobStatus.SentToClient, 2, 9)
        ]);
        var maybe = Request("Spam", JobStatus.RequestReceived, RequestTriage.Maybe, history: [(JobStatus.RequestReceived, 1, 9)]);

        var events = ActivityBuilder.Events(Data([request, maybe]));

        Assert.Equal(
            ["certificates_delivered", "certificates_approved", "certificates_submitted", "inspection_started", "inspector_assigned", "quotation_accepted", "quotation_sent"],
            events.Select(item => item.Code));
        Assert.All(events, item => Assert.Equal(("Delta Steel", request.Number, "Mona Adel"), (item.ClientName, item.RequestNumber, item.ActorName)));
        Assert.Equal(ActivityKind.Quote, events[^1].Kind);
        Assert.Equal(ActivityKind.Job, events[3].Kind);
        Assert.Equal(ActivityKind.Cert, events[0].Kind);
        Assert.DoesNotContain(events, item => item.ClientName == "Spam");
    }

    [Fact]
    public void Several_certificates_approved_on_one_day_are_one_row_with_a_count()
    {
        var request = Request("Delta", JobStatus.CertificateApproved, history: [(JobStatus.CertificateApproved, 3, 11)]);
        var certificates = new[]
        {
            new ExecCertificate(request.Id, null, At(3, 11), CertificateResult.SafeToOperate, Mona, Guid.NewGuid()),
            new ExecCertificate(request.Id, null, At(3, 11), CertificateResult.SafeToOperate, Mona, Guid.NewGuid()),
            new ExecCertificate(request.Id, null, At(3, 12), CertificateResult.NotSafe, Mona, Guid.NewGuid()),
            new ExecCertificate(request.Id, null, At(9, 12), CertificateResult.NotSafe, Mona, Guid.NewGuid())
        };

        var row = Assert.Single(ActivityBuilder.Events(Data([request], certificates: certificates)));

        Assert.Equal(("certificates_approved", 3), (row.Code, row.Count));
    }

    [Fact]
    public void Invoices_payments_and_new_clients_are_events_and_reversals_and_emails_are_not()
    {
        var request = Request("Nile", JobStatus.InProgress);
        var invoice = new ExecInvoice(Guid.NewGuid(), "INV-1", Company, request.Id, Guid.NewGuid(), "Nile", 640_000, At(5), InvoiceStage.Open, false,
            [new ExecPayment(Today.AddDays(-3), 200_000, At(3), null), new ExecPayment(Today.AddDays(-2), -200_000, At(2), Guid.NewGuid())],
            [new ExecInvoiceEvent(InvoiceEventKind.Created, At(5), Mona), new ExecInvoiceEvent(InvoiceEventKind.Emailed, At(4), Mona), new ExecInvoiceEvent(InvoiceEventKind.HandedOver, At(1), Mona)]);
        var client = new ExecClient(Guid.NewGuid(), "Fayoum Mills", null, false, At(6), null, []);

        var events = ActivityBuilder.Events(Data([request], [invoice], clients: [client]));

        Assert.Equal(["invoice_handed_over", "payment_recorded", "invoice_created", "client_added"], events.Select(item => item.Code));
        Assert.Equal(200_000, events[1].AmountPiastres);
        Assert.Equal(640_000, events[2].AmountPiastres);
        Assert.Equal(request.Number, events[2].RequestNumber);
        Assert.Equal(ActivityKind.Client, events[3].Kind);
    }

    [Fact]
    public void A_page_has_a_kind_a_cursor_and_says_where_the_next_page_starts()
    {
        var request = Request("Delta", JobStatus.InProgress, history: [(JobStatus.QuoteSent, 5, 9), (JobStatus.Unassigned, 4, 9), (JobStatus.Assigned, 3, 9), (JobStatus.InProgress, 2, 9)]);
        var events = ActivityBuilder.Events(Data([request]));

        var first = ActivityBuilder.Page(events, null, null, 3);
        var second = ActivityBuilder.Page(events, null, first.NextBefore, 3);
        var quotes = ActivityBuilder.Page(events, ActivityKind.Quote, null, 10);

        Assert.Equal(3, first.Items.Count);
        Assert.NotNull(first.NextBefore);
        Assert.Single(second.Items);
        Assert.Null(second.NextBefore);
        Assert.Equal(2, quotes.Items.Count);
        Assert.Null(ActivityBuilder.ParseKind("all"));
        Assert.Equal(ActivityKind.Inv, ActivityBuilder.ParseKind("inv"));
        Assert.Throws<ArgumentException>(() => ActivityBuilder.ParseKind("everything"));
    }

    [Fact]
    public void Upcoming_has_inspections_and_quotations_expiring_in_the_next_seven_days_only()
    {
        var inspector = Guid.NewGuid();
        var soon = Request("Nile", JobStatus.Assigned, scheduled: At(-1), inspector: inspector);
        var later = Request("Far", JobStatus.Assigned, scheduled: At(-20));
        var done = Request("Done", JobStatus.SentToClient, scheduled: At(-2));
        var expiring = Request("Sinai", JobStatus.QuoteSent);
        var quotes = new[]
        {
            new ExecQuote(Guid.NewGuid(), expiring.Id, 1, QuoteStatus.Open, At(10), null, null, At(-3), false, 420_000, null),
            new ExecQuote(Guid.NewGuid(), expiring.Id, 1, QuoteStatus.Open, At(10), null, null, At(-30), false, 1, null),
            new ExecQuote(Guid.NewGuid(), done.Id, 1, QuoteStatus.Open, At(10), null, null, At(-3), false, 1, null)
        };
        var data = Data([soon, later, done, expiring], quotes: quotes) with { People = [new ExecPerson(inspector, "Omar", UserRole.Inspector, true, null)] };

        var upcoming = ActivityBuilder.Upcoming(data, Today);

        Assert.Equal(2, upcoming.Count);
        Assert.Equal(("inspection", "Nile", "Omar", 3), (upcoming[0].Kind, upcoming[0].ClientName, upcoming[0].InspectorName, upcoming[0].Count));
        Assert.Equal(Today.AddDays(1), upcoming[0].On);
        Assert.Equal(("quotationExpiry", "quotation_expires", 420_000L), (upcoming[1].Kind, upcoming[1].DetailCode, upcoming[1].AmountPiastres));
    }

    [Fact]
    public void An_empty_company_has_an_empty_feed() => Assert.Empty(ActivityBuilder.Events(Data()));
}
