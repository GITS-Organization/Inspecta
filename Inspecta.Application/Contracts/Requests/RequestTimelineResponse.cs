namespace Inspecta.Application.Contracts.Requests;

/// <summary>Everything that happened to a request with its time, and how long each stage took (calendar time).</summary>
/// <remarks>
/// Times are UTC with the offset; durations are whole seconds. <c>stages</c> are always the same seven, in order; a stage the request
/// skipped (no quotation, a job assigned directly) or has not reached yet has <c>null</c> from, to and seconds. A stage that has started and
/// not ended has <c>inProgress</c> and its seconds run until now. <c>totalSeconds</c> runs from the request arriving to <c>finishedAt</c>
/// (sent to the client, or turned down), or until now while it is still open.
/// </remarks>
public sealed record RequestTimelineResponse(
    Guid RequestId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    long TotalSeconds,
    IReadOnlyList<TimelineStage> Stages,
    IReadOnlyList<TimelineEvent> Events,
    TimelineCounters Counters);

/// <param name="Key">
/// <c>quoting</c> (request received to the first offer), <c>client_decision</c> (first offer to accepted), <c>scheduling</c> (accepted to an
/// inspector assigned), <c>waiting_to_start</c> (assigned to inspection started), <c>inspection</c> (started to certificates submitted),
/// <c>review</c> (submitted to approved) and <c>delivery</c> (approved to sent to the client).
/// </param>
/// <param name="Repeats">
/// Extra rounds inside the stage: further offers after the first for <c>client_decision</c>, certificates returned for <c>review</c>; 0 elsewhere.
/// </param>
public sealed record TimelineStage(
    string Key,
    string Title,
    DateTimeOffset? From,
    DateTimeOffset? To,
    long? Seconds,
    bool InProgress,
    int Repeats);

/// <param name="Kind"><c>status</c>, <c>edit</c>, <c>quotation</c>, <c>email</c>, <c>reply</c>, <c>review</c> or <c>delivery</c>.</param>
/// <param name="Actor">Who did it; empty for the system or for the client.</param>
/// <param name="SincePreviousSeconds">Time since the event before it (0 for the first).</param>
public sealed record TimelineEvent(
    DateTimeOffset At,
    string Kind,
    string Title,
    string? Detail,
    TimelineActor? Actor,
    long SincePreviousSeconds);

public sealed record TimelineActor(Guid Id, string Name);

/// <param name="QuoteRevisions">How many quotation revisions were made.</param>
/// <param name="CounterOffers">How many counter offers were sent.</param>
/// <param name="CertificatesReturned">How many times the certificates were sent back to the inspector.</param>
/// <param name="InspectorChanges">How many times the job was handed to another inspector.</param>
public sealed record TimelineCounters(int QuoteRevisions, int CounterOffers, int CertificatesReturned, int InspectorChanges);
