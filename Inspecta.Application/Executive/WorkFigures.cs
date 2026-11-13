using Inspecta.Application.Contracts.Executive;
using Inspecta.Application.Services;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Executive;

/// <summary>
/// How the work flows: jobs in flight, how long steps take, quotations, certificates, the reviewers and the inspectors. Pure, over rows. Emails still marked
/// "maybe" never reach these functions. A step runs from the first time a request entered one status to the first time it entered the next one.
/// </summary>
public static class WorkFigures
{
    public static readonly (string Step, JobStatus[] From, JobStatus[] To)[] Steps =
    [
        ("request_to_quotation_sent", [JobStatus.RequestReceived], [JobStatus.QuoteSent, JobStatus.CounterOfferSent]),
        ("quotation_to_client_answer", [JobStatus.QuoteSent], [JobStatus.Unassigned, JobStatus.QuoteRejected, JobStatus.ClientDeclined, JobStatus.ClientCountered]),
        ("accepted_to_inspector_assigned", [JobStatus.Unassigned], [JobStatus.Assigned]),
        ("inspection_to_submitted", [JobStatus.InProgress], [JobStatus.CertificateSubmitted]),
        ("review_to_approved", [JobStatus.CertificateSubmitted], [JobStatus.CertificateApproved]),
        ("approved_to_delivered", [JobStatus.CertificateApproved], [JobStatus.SentToClient])
    ];

    public static bool InFlight(ExecRequest request) =>
        request.Status is not (JobStatus.QuoteRejected or JobStatus.ClientDeclined or JobStatus.SentToClient);

    public static StageGroup GroupOf(JobStatus status) => status switch
    {
        JobStatus.Unassigned or JobStatus.Assigned or JobStatus.TaskAccepted or JobStatus.InProgress => StageGroup.Field,
        JobStatus.CertificateSubmitted or JobStatus.CertificateReturned => StageGroup.Review,
        JobStatus.CertificateApproved or JobStatus.SentToClient => StageGroup.Delivery,
        _ => StageGroup.Inbox
    };

    public static int AgeDays(ExecRequest request, DateOnly today) => Math.Max(0, today.DayNumber - InvoiceCalendar.Of(request.ReceivedAt).DayNumber);

    /// <summary>Days the request has been in its current status (since its last status change, or since it arrived).</summary>
    public static int DaysInStatus(ExecRequest request, DateOnly today)
    {
        var since = request.Transitions.Where(entry => entry.To == request.Status && entry.From != entry.To).Select(entry => (DateTimeOffset?)entry.At).OrderByDescending(at => at).FirstOrDefault() ?? request.ReceivedAt;
        return Math.Max(0, today.DayNumber - InvoiceCalendar.Of(since).DayNumber);
    }

    public static bool Stuck(ExecRequest request, DateOnly today) => InFlight(request) && DaysInStatus(request, today) >= 30;

    /// <summary>The first time the request entered one of the statuses (a request starts in <c>request_received</c> when it arrived).</summary>
    public static DateTimeOffset? FirstEntry(ExecRequest request, params JobStatus[] statuses)
    {
        var times = request.Transitions.Where(entry => statuses.Contains(entry.To)).Select(entry => (DateTimeOffset?)entry.At).OrderBy(at => at).ToList();
        if (statuses.Contains(JobStatus.RequestReceived) && times.Count == 0) return request.ReceivedAt;
        return times.FirstOrDefault();
    }

    public static IReadOnlyList<PipelineGroup> Pipeline(IEnumerable<ExecRequest> requests)
    {
        var open = requests.Where(InFlight).ToList();
        return Enum.GetValues<StageGroup>().Select(group => new PipelineGroup(group, open.Count(request => GroupOf(request.Status) == group))).ToList();
    }

    public static WorkJobs Jobs(IReadOnlyCollection<ExecRequest> requests, DateOnly from, DateOnly to, DateOnly today)
    {
        var open = requests.Where(InFlight).ToList();
        return new WorkJobs(
            open.Count,
            open.Count == 0 ? 0 : (int)Math.Round(open.Average(request => AgeDays(request, today)), MidpointRounding.AwayFromZero),
            RequestToDelivery(requests, from, to),
            open.Count(request => Stuck(request, today)));
    }

    /// <summary>Standard requests whose certificates reached the client in the period: the average days from the request arriving to delivery.</summary>
    public static int RequestToDelivery(IEnumerable<ExecRequest> requests, DateOnly from, DateOnly to)
    {
        var days = new List<double>();
        foreach (var request in requests.Where(item => item.Kind == RequestKind.Standard))
        {
            var delivered = FirstEntry(request, JobStatus.SentToClient);
            if (delivered is null || !InRange(InvoiceCalendar.Of(delivered.Value), from, to)) continue;
            days.Add(Math.Max(0, (delivered.Value - request.ReceivedAt).TotalDays));
        }

        return days.Count == 0 ? 0 : (int)Math.Round(days.Average(), MidpointRounding.AwayFromZero);
    }

    public static IReadOnlyList<StepTime> StepTimes(IEnumerable<ExecRequest> requests, DateOnly from, DateOnly to, int reviewTargetDays)
    {
        var list = requests.ToList();
        var result = new List<StepTime>();
        foreach (var (step, fromStatuses, toStatuses) in Steps)
        {
            var days = new List<double>();
            foreach (var request in list)
            {
                var start = FirstEntry(request, fromStatuses);
                var end = FirstEntry(request, toStatuses);
                if (start is null || end is null || end < start || !InRange(InvoiceCalendar.Of(end.Value), from, to)) continue;
                days.Add((end.Value - start.Value).TotalDays);
            }

            result.Add(new StepTime(step, days.Count == 0 ? 0 : Math.Round(days.Average(), 1, MidpointRounding.AwayFromZero), step == "review_to_approved" ? reviewTargetDays : null));
        }

        return result;
    }

    public static IReadOnlyList<OpenJob> OpenJobs(IEnumerable<ExecRequest> requests, IEnumerable<ExecPerson> people, DateOnly today)
    {
        var names = people.ToDictionary(person => person.Id, person => person.Name);
        return requests.Where(InFlight)
            .OrderByDescending(request => AgeDays(request, today)).ThenBy(request => request.Id)
            .Select(request => new OpenJob(
                request.Number, request.ClientId, request.ClientName, request.MachineCount,
                request.ReviewerId is Guid reviewer ? names.GetValueOrDefault(reviewer) : null,
                request.Status, GroupOf(request.Status), AgeDays(request, today), Stuck(request, today))).ToList();
    }

    /// <summary>
    /// Quotations in the period. Sent = first versions sent (placeholders left out, revisions not counted again); accepted and rejected are by the day the
    /// client answered, only for quotations that were sent; countered = requests waiting after a counter offer; unanswered over 14 = open sent quotations 14 days
    /// old or more that the client has not countered, with what the client would pay (VAT included).
    /// </summary>
    public static QuotationFigures Quotations(IEnumerable<ExecQuote> quotes, IEnumerable<ExecRequest> requests, IEnumerable<ExecClient> clients, DateOnly from, DateOnly to, DateOnly today)
    {
        var real = quotes.Where(quote => !quote.IsPlaceholder && quote.SentAt is not null).ToList();
        var requestList = requests.ToList();
        var sent = real.Count(quote => quote.Version == 1 && InRange(InvoiceCalendar.Of(quote.SentAt!.Value), from, to));
        var answered = real.Where(quote => quote.ClientRespondedAt is not null && InRange(InvoiceCalendar.Of(quote.ClientRespondedAt.Value), from, to)).ToList();
        var accepted = answered.Count(quote => quote.Status == QuoteStatus.Accepted);
        var rejected = answered.Count(quote => quote.Status == QuoteStatus.Rejected);
        var counteredRequests = requestList.Where(request => request.Status == JobStatus.ClientCountered).Select(request => request.Id).ToHashSet();
        var unanswered = real.Where(quote => quote.Status == QuoteStatus.Open && quote.SupersededAt is null && !counteredRequests.Contains(quote.RequestId)
            && today.DayNumber - InvoiceCalendar.Of(quote.SentAt!.Value).DayNumber >= 14).ToList();
        return new QuotationFigures(
            sent, accepted + rejected == 0 ? 0 : (int)Math.Round(accepted * 100m / (accepted + rejected), MidpointRounding.AwayFromZero),
            counteredRequests.Count, unanswered.Count, unanswered.Sum(quote => quote.TotalWithVatPiastres),
            clients.Count(client => InRange(InvoiceCalendar.Of(client.CreatedAt), from, to)));
    }

    /// <summary>Certificates approved in each of the last twelve months, by their verdict (a certificate with no verdict is counted in none).</summary>
    public static IReadOnlyList<CertificatesMonth> CertificatesByMonth(IEnumerable<ExecCertificate> certificates, DateOnly today)
    {
        var approved = certificates.Where(certificate => certificate.IsIssued).Select(certificate => (Day: InvoiceCalendar.Of(certificate.ReviewedAt!.Value), certificate.Result)).ToList();
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
        return Enumerable.Range(0, 12).Select(index => first.AddMonths(index)).Select(month =>
        {
            var inMonth = approved.Where(item => item.Day.Year == month.Year && item.Day.Month == month.Month).ToList();
            return new CertificatesMonth(
                $"{month.Year:D4}-{month.Month:D2}",
                inMonth.Count(item => item.Result == CertificateResult.SafeToOperate),
                inMonth.Count(item => item.Result == CertificateResult.SafeWithConditions),
                inMonth.Count(item => item.Result == CertificateResult.NotSafe));
        }).ToList();
    }

    public static int CertificatesIssued(IEnumerable<ExecCertificate> certificates, DateOnly from, DateOnly to) =>
        certificates.Count(certificate => certificate.IsIssued && InRange(InvoiceCalendar.Of(certificate.ReviewedAt.Value), from, to));

    public static int SafePercentOfIssued(IEnumerable<ExecCertificate> certificates, DateOnly from, DateOnly to)
    {
        var issued = certificates.Where(certificate => certificate.IsIssued && InRange(InvoiceCalendar.Of(certificate.ReviewedAt.Value), from, to)).ToList();
        return issued.Count == 0 ? 0 : (int)Math.Round(issued.Count(certificate => certificate.Result == CertificateResult.SafeToOperate) * 100m / issued.Count, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The active technical managers: certificates they approved in the period, jobs waiting for them, and the average days of the jobs they approved in the
    /// period from the first submission to the approval. <c>Slow</c> is above the review target.
    /// </summary>
    public static IReadOnlyList<ReviewerRow> Reviewers(
        IEnumerable<ExecPerson> people, IReadOnlyCollection<ExecRequest> requests, IReadOnlyCollection<ExecCertificate> certificates, DateOnly from, DateOnly to, int targetDays)
    {
        var byId = requests.ToDictionary(request => request.Id);
        return people.Where(person => person.Role == UserRole.TechnicalManager && person.IsActive).Select(person =>
        {
            var approved = certificates.Where(certificate => certificate.ReviewerId == person.Id && certificate.IsIssued
                && InRange(InvoiceCalendar.Of(certificate.ReviewedAt.Value), from, to)).ToList();
            var days = new List<double>();
            foreach (var requestId in approved.Select(certificate => certificate.RequestId).Distinct())
            {
                if (!byId.TryGetValue(requestId, out var request)) continue;
                var start = FirstEntry(request, JobStatus.CertificateSubmitted);
                var end = FirstEntry(request, JobStatus.CertificateApproved);
                if (start is not null && end is not null && end >= start) days.Add((end.Value - start.Value).TotalDays);
            }

            var average = days.Count == 0 ? 0 : Math.Round(days.Average(), 1, MidpointRounding.AwayFromZero);
            return new ReviewerRow(
                person.Id, person.Name, approved.Count, requests.Count(request => request.Status == JobStatus.CertificateSubmitted && request.ReviewerId == person.Id), average, average > targetDays);
        }).OrderByDescending(row => row.Certificates).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<InspectorRow> Inspectors(
        IEnumerable<ExecPerson> people, IReadOnlyCollection<ExecRequest> requests, IReadOnlyCollection<ExecCertificate> certificates, DateOnly from, DateOnly to) =>
        people.Where(person => person.Role == UserRole.Inspector && person.IsActive).Select(person => new InspectorRow(
            person.Id, person.Name,
            certificates.Count(certificate => certificate.InspectorId == person.Id && certificate.IsIssued && InRange(InvoiceCalendar.Of(certificate.ReviewedAt.Value), from, to)),
            requests.Count(request => InFlight(request) && request.InspectorId == person.Id),
            person.OnLeaveUntil is DateTimeOffset until && InvoiceCalendar.Of(until) >= InvoiceCalendar.Today() ? InvoiceCalendar.Of(until) : null))
            .OrderByDescending(row => row.Certificates).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToList();

    private static bool InRange(DateOnly day, DateOnly from, DateOnly to) => day >= from && day <= to;
}
