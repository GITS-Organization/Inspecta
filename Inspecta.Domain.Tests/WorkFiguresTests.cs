using Inspecta.Application.Contracts.Executive;
using Inspecta.Application.Executive;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>How the work flows: jobs, steps, quotations, certificates, reviewers and inspectors, one definition at a time over hand-made rows.</summary>
public sealed class WorkFiguresTests
{
    private static readonly DateOnly Today = new(2026, 11, 12);
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    private static DateTimeOffset At(int daysAgo, int hour = 10) => new(Today.AddDays(-daysAgo).ToDateTime(new TimeOnly(hour, 0)), TimeSpan.FromHours(2));

    private static ExecRequest Request(
        JobStatus status, int receivedDaysAgo, RequestKind kind = RequestKind.Standard, Guid? reviewer = null, Guid? inspector = null,
        params (JobStatus To, int DaysAgo)[] history) =>
        new(Guid.NewGuid(), Company, Guid.NewGuid(), "Client", kind, RequestTriage.Request, status, reviewer, null, inspector, At(receivedDaysAgo), null, 2, [],
            history.Select(entry => new ExecTransition(null, entry.To, At(entry.DaysAgo), Actor)).ToList());

    private static ExecQuote Quote(QuoteStatus status, int? sentDaysAgo, int? answeredDaysAgo = null, int version = 1, bool placeholder = false, long total = 1_000, Guid? requestId = null, bool superseded = false) =>
        new(Guid.NewGuid(), requestId ?? Guid.NewGuid(), version, status, sentDaysAgo is int s ? At(s) : null, answeredDaysAgo is int a ? At(a) : null,
            superseded ? At(1) : null, At(-10), placeholder, total, null);

    private static ExecCertificate Certificate(int? approvedDaysAgo, CertificateResult? result = CertificateResult.SafeToOperate, Guid? reviewer = null, Guid? inspector = null, Guid? requestId = null) =>
        new(requestId ?? Guid.NewGuid(), null, approvedDaysAgo is int d ? At(d) : null, result, reviewer, inspector ?? Guid.NewGuid());

    [Fact]
    public void In_flight_leaves_out_rejected_declined_and_delivered_and_the_pipeline_groups_the_rest()
    {
        var requests = new[]
        {
            Request(JobStatus.RequestReceived, 1), Request(JobStatus.QuoteSent, 2), Request(JobStatus.ClientCountered, 2), Request(JobStatus.Assigned, 3),
            Request(JobStatus.InProgress, 3), Request(JobStatus.CertificateSubmitted, 4), Request(JobStatus.CertificateReturned, 4), Request(JobStatus.CertificateApproved, 5),
            Request(JobStatus.SentToClient, 6), Request(JobStatus.QuoteRejected, 6), Request(JobStatus.ClientDeclined, 6)
        };

        var pipeline = WorkFigures.Pipeline(requests).ToDictionary(item => item.Group, item => item.Count);

        Assert.Equal((3, 2, 2, 1), (pipeline[StageGroup.Inbox], pipeline[StageGroup.Field], pipeline[StageGroup.Review], pipeline[StageGroup.Delivery]));
        Assert.Equal(8, WorkFigures.Jobs(requests, Today.AddDays(-30), Today, Today).InFlight);
    }

    [Fact]
    public void Stuck_means_30_days_or_more_in_the_same_status_and_age_counts_from_arrival()
    {
        var stuck = Request(JobStatus.CertificateReturned, 50, history: [(JobStatus.CertificateReturned, 31)]);
        var fresh = Request(JobStatus.CertificateReturned, 50, history: [(JobStatus.CertificateReturned, 29)]);
        var neverMoved = Request(JobStatus.RequestReceived, 31);
        var delivered = Request(JobStatus.SentToClient, 90, history: [(JobStatus.SentToClient, 80)]);

        Assert.True(WorkFigures.Stuck(stuck, Today));
        Assert.False(WorkFigures.Stuck(fresh, Today));
        Assert.True(WorkFigures.Stuck(neverMoved, Today));
        Assert.False(WorkFigures.Stuck(delivered, Today));
        var jobs = WorkFigures.Jobs([stuck, fresh, neverMoved, delivered], Today.AddDays(-30), Today, Today);
        Assert.Equal(2, jobs.StuckOver30);
        Assert.Equal(44, jobs.AverageAgeDays);
    }

    [Fact]
    public void Request_to_delivery_counts_only_standard_requests_delivered_in_the_period()
    {
        var delivered = Request(JobStatus.SentToClient, 30, history: [(JobStatus.SentToClient, 4)]);
        var slower = Request(JobStatus.SentToClient, 40, history: [(JobStatus.SentToClient, 10)]);
        var manDay = Request(JobStatus.SentToClient, 40, RequestKind.ManDay, history: [(JobStatus.SentToClient, 2)]);
        var before = Request(JobStatus.SentToClient, 100, history: [(JobStatus.SentToClient, 60)]);

        var days = WorkFigures.RequestToDelivery([delivered, slower, manDay, before], Today.AddDays(-20), Today);

        Assert.Equal(28, days);
    }

    [Fact]
    public void Step_times_run_from_the_first_entry_of_a_status_to_the_first_entry_of_the_next_so_loops_do_not_restart_the_clock()
    {
        var request = Request(JobStatus.CertificateApproved, 30, history:
        [
            (JobStatus.QuoteDraft, 29), (JobStatus.QuoteSent, 28), (JobStatus.QuoteDraft, 27), (JobStatus.QuoteSent, 26), (JobStatus.Unassigned, 24),
            (JobStatus.Assigned, 22), (JobStatus.InProgress, 20), (JobStatus.CertificateSubmitted, 14), (JobStatus.CertificateReturned, 12),
            (JobStatus.InProgress, 11), (JobStatus.CertificateSubmitted, 9), (JobStatus.CertificateApproved, 7)
        ]);

        var steps = WorkFigures.StepTimes([request], Today.AddDays(-40), Today, reviewTargetDays: 2).ToDictionary(step => step.Step);

        Assert.Equal(2.0, steps["request_to_quotation_sent"].AverageDays);
        Assert.Equal(4.0, steps["quotation_to_client_answer"].AverageDays);
        Assert.Equal(2.0, steps["accepted_to_inspector_assigned"].AverageDays);
        Assert.Equal(6.0, steps["inspection_to_submitted"].AverageDays);
        Assert.Equal(7.0, steps["review_to_approved"].AverageDays);
        Assert.Equal(2, steps["review_to_approved"].TargetDays);
        Assert.Null(steps["request_to_quotation_sent"].TargetDays);
        Assert.Equal(0.0, steps["approved_to_delivered"].AverageDays);
    }

    [Fact]
    public void A_step_counts_in_the_period_in_which_it_finished()
    {
        var request = Request(JobStatus.CertificateApproved, 60, history: [(JobStatus.CertificateSubmitted, 40), (JobStatus.CertificateApproved, 38)]);

        Assert.Equal(2.0, WorkFigures.StepTimes([request], Today.AddDays(-45), Today.AddDays(-35), 2).Single(step => step.Step == "review_to_approved").AverageDays);
        Assert.Equal(0.0, WorkFigures.StepTimes([request], Today.AddDays(-10), Today, 2).Single(step => step.Step == "review_to_approved").AverageDays);
    }

    [Fact]
    public void Quotations_count_first_versions_exclude_placeholders_and_price_the_unanswered_with_vat()
    {
        var counteredRequest = Request(JobStatus.ClientCountered, 20);
        var quotes = new[]
        {
            Quote(QuoteStatus.Open, sentDaysAgo: 3),
            Quote(QuoteStatus.Open, sentDaysAgo: 4, version: 2),
            Quote(QuoteStatus.Accepted, sentDaysAgo: 8, answeredDaysAgo: 5),
            Quote(QuoteStatus.Accepted, sentDaysAgo: 9, answeredDaysAgo: 2),
            Quote(QuoteStatus.Accepted, sentDaysAgo: 9, answeredDaysAgo: 2, placeholder: true),
            Quote(QuoteStatus.Rejected, sentDaysAgo: 7, answeredDaysAgo: 1),
            Quote(QuoteStatus.Rejected, sentDaysAgo: null, answeredDaysAgo: 1),
            Quote(QuoteStatus.Open, sentDaysAgo: 14, total: 2_280),
            Quote(QuoteStatus.Open, sentDaysAgo: 13, total: 9_999),
            Quote(QuoteStatus.Open, sentDaysAgo: 20, total: 5_000, requestId: counteredRequest.Id),
            Quote(QuoteStatus.Open, sentDaysAgo: 30, total: 7_000, superseded: true)
        };
        var clients = new[] { new ExecClient(Guid.NewGuid(), "New", null, false, At(3), null, []), new ExecClient(Guid.NewGuid(), "Old", null, false, At(300), null, []) };

        var figures = WorkFigures.Quotations(quotes, [counteredRequest], clients, Today.AddDays(-10), Today, Today);

        Assert.Equal(4, figures.Sent);
        Assert.Equal(67, figures.AcceptedPercent);
        Assert.Equal(1, figures.Countered);
        Assert.Equal(1, figures.UnansweredOver14);
        Assert.Equal(2_280, figures.UnansweredOver14Piastres);
        Assert.Equal(1, figures.NewClients);
    }

    [Fact]
    public void Certificates_by_month_count_approved_ones_by_verdict_over_the_last_twelve_months()
    {
        var certificates = new[]
        {
            Certificate(2), Certificate(3), Certificate(4, CertificateResult.NotSafe), Certificate(50, CertificateResult.SafeWithConditions),
            Certificate(null), Certificate(400)
        };

        var months = WorkFigures.CertificatesByMonth(certificates, Today);

        Assert.Equal(12, months.Count);
        Assert.Equal((2, 0, 1), (months[11].Safe, months[11].WithConditions, months[11].NotSafe));
        Assert.Equal((0, 1, 0), (months[9].Safe, months[9].WithConditions, months[9].NotSafe));
        Assert.Equal(4, WorkFigures.CertificatesIssued(certificates, Today.AddDays(-60), Today));
        Assert.Equal(50, WorkFigures.SafePercentOfIssued(certificates, Today.AddDays(-60), Today));
    }

    [Fact]
    public void Reviewers_show_what_they_approved_what_waits_for_them_and_their_review_time_against_the_target()
    {
        var quick = Guid.NewGuid();
        var slow = Guid.NewGuid();
        var people = new[]
        {
            new ExecPerson(quick, "Quick", UserRole.TechnicalManager, true, null), new ExecPerson(slow, "Slow", UserRole.TechnicalManager, true, null),
            new ExecPerson(Guid.NewGuid(), "Gone", UserRole.TechnicalManager, false, null), new ExecPerson(Guid.NewGuid(), "Coordinator", UserRole.Coordinator, true, null)
        };
        var quickJob = Request(JobStatus.CertificateApproved, 20, reviewer: quick, history: [(JobStatus.CertificateSubmitted, 6), (JobStatus.CertificateApproved, 5)]);
        var slowJob = Request(JobStatus.CertificateApproved, 20, reviewer: slow, history: [(JobStatus.CertificateSubmitted, 9), (JobStatus.CertificateApproved, 5)]);
        var waiting = Request(JobStatus.CertificateSubmitted, 5, reviewer: slow);
        var certificates = new[]
        {
            Certificate(5, reviewer: quick, requestId: quickJob.Id), Certificate(5, reviewer: quick, requestId: quickJob.Id), Certificate(5, reviewer: slow, requestId: slowJob.Id)
        };

        var rows = WorkFigures.Reviewers(people, [quickJob, slowJob, waiting], certificates, Today.AddDays(-30), Today, targetDays: 2);

        Assert.Equal(["Quick", "Slow"], rows.Select(row => row.Name));
        Assert.Equal((2, 0, 1.0, false), (rows[0].Certificates, rows[0].Waiting, rows[0].AverageReviewDays, rows[0].Slow));
        Assert.Equal((1, 1, 4.0, true), (rows[1].Certificates, rows[1].Waiting, rows[1].AverageReviewDays, rows[1].Slow));
    }

    [Fact]
    public void Inspectors_show_certificates_jobs_in_hand_and_a_leave_that_has_not_ended()
    {
        var busy = Guid.NewGuid();
        var away = Guid.NewGuid();
        var back = Guid.NewGuid();
        var people = new[]
        {
            new ExecPerson(busy, "Busy", UserRole.Inspector, true, null),
            new ExecPerson(away, "Away", UserRole.Inspector, true, At(-8)),
            new ExecPerson(back, "Back", UserRole.Inspector, true, At(3))
        };
        var requests = new[] { Request(JobStatus.InProgress, 3, inspector: busy), Request(JobStatus.Assigned, 2, inspector: busy), Request(JobStatus.SentToClient, 9, inspector: busy) };
        var certificates = new[] { Certificate(2, inspector: busy), Certificate(40, inspector: busy) };

        var rows = WorkFigures.Inspectors(people, requests, certificates, Today.AddDays(-30), Today).ToDictionary(row => row.Name);

        Assert.Equal((1, 2), (rows["Busy"].Certificates, rows["Busy"].Jobs));
        Assert.Equal(Today.AddDays(8), rows["Away"].OnLeaveUntil);
        Assert.Null(rows["Back"].OnLeaveUntil);
    }
}
