using Inspecta.Application.Contracts.Requests;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

/// <summary>The stages of a request with their durations, and when it started and finished.</summary>
public sealed record RequestStageResult(IReadOnlyList<TimelineStage> Stages, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt);

/// <summary>
/// Works out how long each stage of a request took from its status history. Only real status changes count (an entry whose from and to are
/// the same status is an edit, not a stage). Where a stage repeats, it runs from its first start to its first end.
/// </summary>
public static class RequestStages
{
    private static readonly (string Key, string Title)[] Order =
    [
        ("quoting", "Request received to first offer"),
        ("client_decision", "First offer to the client's decision"),
        ("scheduling", "Accepted to inspector assigned"),
        ("waiting_to_start", "Assigned to inspection started"),
        ("inspection", "Inspection started to certificates submitted"),
        ("review", "Certificates submitted to approved"),
        ("delivery", "Approved to sent to the client")
    ];

    public static RequestStageResult Calculate(IReadOnlyList<RequestHistoryResponse> history, DateTimeOffset now)
    {
        var changes = history.Where(entry => entry.From != entry.To).OrderBy(entry => entry.At).ToList();

        DateTimeOffset? First(params JobStatus[] statuses) =>
            changes.Where(entry => statuses.Contains(entry.To)).Select(entry => (DateTimeOffset?)entry.At).FirstOrDefault();

        static DateTimeOffset? Earliest(params DateTimeOffset?[] times) => times.Where(time => time is not null).Min();

        var startedAt = First(JobStatus.RequestReceived) ?? changes.Select(entry => (DateTimeOffset?)entry.At).FirstOrDefault();
        var hadQuotation = changes.Any(entry => entry.To is JobStatus.QuoteDraft or JobStatus.QuoteSent or JobStatus.CounterOfferSent);
        var firstOffer = First(JobStatus.QuoteSent, JobStatus.CounterOfferSent);
        var turnedDown = First(JobStatus.QuoteRejected, JobStatus.ClientDeclined);
        var accepted = First(JobStatus.Unassigned);
        var firstAssigned = First(JobStatus.Assigned);
        var inspectionStarted = First(JobStatus.InProgress);
        var submitted = First(JobStatus.CertificateSubmitted);
        var approved = First(JobStatus.CertificateApproved);
        var sent = First(JobStatus.SentToClient);
        var finishedAt = sent ?? turnedDown;

        // The job went straight to an inspector or to inspection (man day, contract client, direct certificate): there was no quotation.
        // A request that has only been received has not skipped anything yet, so its quoting stage is the one running.
        var skippedQuotation = !hadQuotation && accepted is null && (firstAssigned is not null || inspectionStarted is not null);
        var quotingApplies = !skippedQuotation && startedAt is not null;

        // A new offer always comes through a draft; going back from "client answered" to "sent" only dismisses the client's reply.
        var offers = changes.Count(entry => entry.To is JobStatus.QuoteSent or JobStatus.CounterOfferSent && entry.From != JobStatus.ClientCountered);
        var returned = changes.Count(entry => entry.To == JobStatus.CertificateReturned);

        var bounds = new (DateTimeOffset? From, DateTimeOffset? To, int Repeats)[]
        {
            (quotingApplies ? startedAt : null, quotingApplies ? Earliest(firstOffer, turnedDown) : null, 0),
            (firstOffer, firstOffer is null ? null : Earliest(accepted, turnedDown), Math.Max(0, offers - 1)),
            (accepted ?? (skippedQuotation && firstAssigned is not null ? startedAt : null), firstAssigned, 0),
            (firstAssigned, inspectionStarted, 0),
            (inspectionStarted, submitted, 0),
            (submitted, approved, returned),
            (approved, sent, 0)
        };

        // The stage that is running is the last one that has started and not ended, and only while the request is still open.
        var lastStarted = finishedAt is null ? Array.FindLastIndex(bounds, bound => bound.From is not null) : -1;
        var stages = bounds.Select((bound, index) =>
        {
            var running = index == lastStarted && bound.To is null;
            long? seconds = bound.From is { } from
                ? bound.To is { } to ? (long)(to - from).TotalSeconds : running ? (long)(now - from).TotalSeconds : null
                : null;
            return new TimelineStage(Order[index].Key, Order[index].Title, bound.From, bound.To, seconds, running, bound.Repeats);
        }).ToArray();

        return new RequestStageResult(stages, startedAt, finishedAt);
    }
}
