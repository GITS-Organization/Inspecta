using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

public sealed class InspectionRequest
{
    private static readonly IReadOnlyDictionary<JobStatus, JobStatus[]> AllowedTransitions =
        new Dictionary<JobStatus, JobStatus[]>
        {
            [JobStatus.RequestReceived] = [JobStatus.QuoteDraft],
            [JobStatus.QuoteDraft] = [JobStatus.QuoteSent],
            [JobStatus.QuoteSent] = [JobStatus.ClientCountered, JobStatus.QuoteRejected, JobStatus.QuoteAccepted, JobStatus.ClientDeclined],
            [JobStatus.ClientCountered] = [JobStatus.QuoteDraft],
            [JobStatus.QuoteAccepted] = [JobStatus.Assigned],
            [JobStatus.Assigned] = [JobStatus.TaskAccepted],
            [JobStatus.TaskAccepted] = [JobStatus.InProgress],
            [JobStatus.InProgress] = [JobStatus.CertificateSubmitted],
            [JobStatus.CertificateSubmitted] = [JobStatus.CertificateReturned, JobStatus.CertificateApproved],
            [JobStatus.CertificateReturned] = [JobStatus.InProgress],
            [JobStatus.CertificateApproved] = [JobStatus.SentToClient]
        };

    private InspectionRequest() { }

    public Guid Id { get; private set; }
    public string? LegacyId { get; private set; }
    public string ClientName { get; private set; } = string.Empty;
    public Guid? ClientId { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public string? Subject { get; private set; }
    public string? EmailThreadId { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? PreferredDate { get; private set; }
    public string? AccessNotes { get; private set; }
    public JobStatus Status { get; private set; }
    public Guid? CoordinatorId { get; private set; }
    public Guid? InspectorId { get; private set; }
    public DateTimeOffset? ScheduledAt { get; private set; }
    public string? AssignmentNote { get; private set; }
    public string? RejectReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public List<RequestItem> Items { get; private set; } = [];
    public List<JobStatusHistory> History { get; private set; } = [];

    public static InspectionRequest Create(
        string clientName,
        string location,
        Guid? clientId,
        IEnumerable<RequestItem> items,
        Guid coordinatorId,
        string? legacyId = null,
        string? subject = null,
        string? emailThreadId = null,
        DateTimeOffset? preferredDate = null,
        string? accessNotes = null)
    {
        if (string.IsNullOrWhiteSpace(clientName)) throw new ArgumentException("Client name is required.", nameof(clientName));
        if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("Location is required.", nameof(location));

        var requestItems = items.ToList();
        if (requestItems.Count == 0) throw new ArgumentException("At least one item is required.", nameof(items));

        var now = DateTimeOffset.UtcNow;
        var request = new InspectionRequest
        {
            Id = Guid.NewGuid(),
            LegacyId = legacyId,
            ClientName = clientName.Trim(),
            ClientId = clientId,
            Location = location.Trim(),
            Subject = subject?.Trim(),
            EmailThreadId = emailThreadId,
            ReceivedAt = now,
            PreferredDate = preferredDate,
            AccessNotes = accessNotes,
            CoordinatorId = coordinatorId,
            Status = JobStatus.RequestReceived,
            CreatedAt = now,
            UpdatedAt = now
        };
        request.Items.AddRange(requestItems);
        request.History.Add(new JobStatusHistory(request.Id, null, JobStatus.RequestReceived, coordinatorId, now, null));
        return request;
    }

    public void SetClient(Guid clientId)
    {
        if (Status is not (JobStatus.RequestReceived or JobStatus.QuoteDraft))
            throw new InvalidOperationException("Client can only be matched before a quote is sent.");

        ClientId = clientId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AssignInspector(Guid inspectorId, DateTimeOffset scheduledAt, Guid coordinatorId, string? note = null)
    {
        if (Status != JobStatus.QuoteAccepted)
            throw new InvalidOperationException("Only accepted jobs can be assigned.");
        if (inspectorId == Guid.Empty) throw new ArgumentException("Inspector is required.", nameof(inspectorId));

        InspectorId = inspectorId;
        ScheduledAt = scheduledAt;
        AssignmentNote = note;
        TransitionTo(JobStatus.Assigned, coordinatorId, note);
    }

    public void TransitionTo(JobStatus nextStatus, Guid actorId, string? note = null)
    {
        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(nextStatus))
            throw new InvalidOperationException($"Transition from {Status} to {nextStatus} is not allowed.");

        if (nextStatus == JobStatus.QuoteSent && (ClientId is null || Items.Count == 0 || string.IsNullOrWhiteSpace(Location)))
            throw new InvalidOperationException("A matched client, at least one item, and a location are required before quoting.");
        if (nextStatus == JobStatus.CertificateSubmitted && InspectorId is null)
            throw new InvalidOperationException("An assigned inspector is required before certificate submission.");
        if (nextStatus == JobStatus.QuoteRejected && string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException("A rejection reason is required.");

        var previousStatus = Status;
        Status = nextStatus;
        if (nextStatus == JobStatus.QuoteRejected) RejectReason = note!.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
        History.Add(new JobStatusHistory(Id, previousStatus, nextStatus, actorId, UpdatedAt, note));
    }
}