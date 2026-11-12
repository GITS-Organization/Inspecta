using System.Globalization;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

/// <summary>Everything stored about one request that belongs on its timeline.</summary>
public sealed record RequestTimelineSources(
    InspectionRequest Request,
    IReadOnlyList<Quote> Quotes,
    IReadOnlyList<OutboundEmailSummary> Emails,
    IReadOnlyList<ClientReply> Replies,
    CertificateDelivery? Delivery,
    IReadOnlyDictionary<Guid, string> Names);

/// <summary>
/// Puts the status history, the quotations, the emails, the client's replies, the review comments and the delivery of one request into a
/// single list, oldest first, with who did each and the time since the one before. A fact the status history already records (an offer
/// being sent) is listed once: the quotation adds its figures to that event instead of a second line.
/// </summary>
public static class RequestTimelineBuilder
{
    // A quotation is stamped as sent in the same save as its status change, so the two are seconds apart at most.
    private static readonly TimeSpan SameMoment = TimeSpan.FromMinutes(2);

    public static RequestTimelineResponse Build(RequestTimelineSources sources, DateTimeOffset now)
    {
        var history = sources.Request.History.OrderBy(entry => entry.OccurredAt)
            .Select(entry => new RequestHistoryResponse(entry.FromStatus, entry.ToStatus, entry.ActorId, entry.OccurredAt, entry.Note))
            .ToArray();
        var stages = RequestStages.Calculate(history, now);

        var raw = new List<Raw>();
        var offerEvents = new List<Raw>();
        foreach (var entry in history)
        {
            var item = FromHistory(entry);
            raw.Add(item);
            if (entry.To is JobStatus.QuoteSent or JobStatus.CounterOfferSent && entry.From != entry.To) offerEvents.Add(item);
        }

        AddQuotations(sources, raw, offerEvents);

        foreach (var email in sources.Emails)
        {
            var who = string.IsNullOrWhiteSpace(email.ToAddress) ? "the client" : email.ToAddress;
            var what = email.Kind == OutboundEmailKind.Accept ? "Acceptance email" : "Offer email";
            if (email.Status == OutboundEmailStatus.Sent)
                raw.Add(new Raw(email.SentAt ?? email.CreatedAt, "email", $"{what} sent to {who}", email.Subject, null));
            else if (email.Status == OutboundEmailStatus.Failed)
                raw.Add(new Raw(email.CreatedAt, "email", $"{what} to {who} failed", email.LastError, null));
        }

        foreach (var reply in sources.Replies)
            raw.Add(new Raw(reply.ReceivedAt, "reply", "Client replied by email", reply.Subject, null));

        foreach (var comment in sources.Request.ReviewComments)
            raw.Add(new Raw(comment.CreatedAt, "review", "Review comment", comment.Text, comment.ReviewerId));

        if (sources.Delivery is { Sent: true, SentAt: { } deliveredAt } delivery)
            raw.Add(new Raw(deliveredAt, "delivery", "Certificates emailed to the client", delivery.ToAddress, null));

        var ordered = raw.Select((item, index) => (item, index)).OrderBy(pair => pair.item.At).ThenBy(pair => pair.index).Select(pair => pair.item).ToList();

        var events = new List<TimelineEvent>(ordered.Count);
        DateTimeOffset? previous = null;
        foreach (var item in ordered)
        {
            var since = previous is { } before ? Math.Max(0L, (long)(item.At - before).TotalSeconds) : 0L;
            var actor = item.ActorId is { } id && id != Guid.Empty
                ? new TimelineActor(id, sources.Names.TryGetValue(id, out var name) ? name : "Unknown user")
                : null;
            events.Add(new TimelineEvent(item.At, item.Kind, item.Title, item.Detail, actor, since));
            previous = item.At;
        }

        var startedAt = stages.StartedAt ?? sources.Request.ReceivedAt;
        var end = stages.FinishedAt ?? now;

        return new RequestTimelineResponse(
            sources.Request.Id,
            stages.StartedAt,
            stages.FinishedAt,
            stages.StartedAt is null ? 0 : Math.Max(0L, (long)(end - startedAt).TotalSeconds),
            stages.Stages,
            events,
            Counters(sources, history));
    }

    private static void AddQuotations(RequestTimelineSources sources, List<Raw> raw, List<Raw> offerEvents)
    {
        var unmatched = offerEvents.ToList();
        foreach (var quote in sources.Quotes.OrderBy(quote => quote.Version))
        {
            if (quote.Version > 1)
                raw.Add(new Raw(quote.CreatedAt, "quotation", $"Quotation revision {quote.Version} created", null, quote.CreatedBy));

            if (quote.SentAt is { } sentAt)
            {
                var figures = Figures(quote);
                var match = unmatched
                    .Where(item => (item.At - sentAt).Duration() <= SameMoment)
                    .OrderBy(item => (item.At - sentAt).Duration())
                    .FirstOrDefault();
                if (match is not null)
                {
                    unmatched.Remove(match);
                    match.Detail = string.IsNullOrWhiteSpace(match.Detail) ? figures : $"{figures}. {match.Detail}";
                }
                else
                {
                    raw.Add(new Raw(sentAt, "quotation", $"Quotation revision {quote.Version} sent", figures, quote.CreatedBy));
                }
            }

            if (quote.ClientRespondedAt is { } respondedAt)
            {
                var detail = quote.ClientCounterPricePiastres is { } counter ? $"Client counter price {Money(counter)}" : null;
                raw.Add(new Raw(respondedAt, "quotation", $"Client answered quotation revision {quote.Version}", detail, null));
            }

            if (quote.LastReminderAt is { } reminderAt)
                raw.Add(new Raw(reminderAt, "quotation", $"Reminder about quotation revision {quote.Version}", null, null));
        }
    }

    private static Raw FromHistory(RequestHistoryResponse entry)
    {
        if (IsInspectorChange(entry))
            return new Raw(entry.At, "status", "Job handed to another inspector", entry.Note, entry.ActorId);
        if (entry.From == entry.To)
            return new Raw(entry.At, "edit", string.IsNullOrWhiteSpace(entry.Note) ? "Request edited" : entry.Note!, null, entry.ActorId);
        return new Raw(entry.At, "status", Describe(entry.To), entry.Note, entry.ActorId);
    }

    private static bool IsInspectorChange(RequestHistoryResponse entry) =>
        entry.To == JobStatus.Assigned &&
        (entry.From == JobStatus.TaskAccepted ||
         entry.From == JobStatus.Assigned && entry.Note?.StartsWith("Inspection date changed", StringComparison.Ordinal) != true);

    private static TimelineCounters Counters(RequestTimelineSources sources, IReadOnlyList<RequestHistoryResponse> history) => new(
        QuoteRevisions: sources.Quotes.Count(quote => quote.Version > 1),
        CounterOffers: history.Count(entry => entry.To == JobStatus.CounterOfferSent && entry.From != entry.To),
        CertificatesReturned: history.Count(entry => entry.To == JobStatus.CertificateReturned),
        InspectorChanges: history.Count(IsInspectorChange));

    private static string Figures(Quote quote)
    {
        var text = $"Revision {quote.Version}, total {Money(quote.AmountPiastres)}";
        return quote.DiscountPercent > 0
            ? $"{text} after a {quote.DiscountPercent.ToString("0.##", CultureInfo.InvariantCulture)}% discount of {Money(quote.DiscountPiastres)}"
            : text;
    }

    private static string Money(long piastres) => (piastres / 100m).ToString("N2", CultureInfo.InvariantCulture);

    private static string Describe(JobStatus status) => status switch
    {
        JobStatus.RequestReceived => "Request received",
        JobStatus.QuoteDraft => "Quotation being prepared",
        JobStatus.QuoteSent => "Quotation sent to the client",
        JobStatus.CounterOfferSent => "Counter offer sent to the client",
        JobStatus.ClientCountered => "Client answered the offer",
        JobStatus.QuoteRejected => "Quotation turned down",
        JobStatus.ClientDeclined => "Client declined",
        JobStatus.Unassigned => "Offer accepted, waiting for an inspector",
        JobStatus.Assigned => "Given to an inspector",
        JobStatus.TaskAccepted => "Inspector accepted the job",
        JobStatus.InProgress => "Inspection started",
        JobStatus.CertificateSubmitted => "Certificates submitted for review",
        JobStatus.CertificateReturned => "Certificates sent back to the inspector",
        JobStatus.CertificateApproved => "Certificates approved",
        JobStatus.SentToClient => "Certificates sent to the client",
        _ => status.ToString()
    };

    private sealed class Raw(DateTimeOffset at, string kind, string title, string? detail, Guid? actorId)
    {
        public DateTimeOffset At { get; } = at;
        public string Kind { get; } = kind;
        public string Title { get; } = title;
        public string? Detail { get; set; } = detail;
        public Guid? ActorId { get; } = actorId;
    }
}
