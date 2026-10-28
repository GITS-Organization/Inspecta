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
    public Guid? ManagerId { get; private set; }
    public string ClientName { get; private set; } = string.Empty;
    public Guid? ClientId { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public string? Subject { get; private set; }
    public string? EmailThreadId { get; private set; }
    public string? EmailFrom { get; private set; }
    public string? EmailFromName { get; private set; }
    public string? EmailPreview { get; private set; }
    public RequestSource Source { get; private set; } = RequestSource.Manual;
    public bool HasUnreadClientReply { get; private set; }
    public string? SeededBy { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? PreferredDate { get; private set; }
    public string? AccessNotes { get; private set; }
    public DateTimeOffset? CertificateSubmittedAt { get; private set; }
    public JobStatus Status { get; private set; }
    public Guid? CoordinatorId { get; private set; }
    public Guid? InspectorId { get; private set; }
    public DateTimeOffset? ScheduledAt { get; private set; }
    public string? AssignmentNote { get; private set; }
    public string? RejectReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Client? Client { get; private set; }
    public List<InspectionCertificate> Certificates { get; private set; } = [];
    public List<RequestItem> Items { get; private set; } = [];
    public List<JobStatusHistory> History { get; private set; } = [];
    public RequestEmail? Email { get; private set; }

    public static InspectionRequest Create(
        string clientName,
        string location,
        Guid? clientId,
        IEnumerable<RequestItem> items,
        Guid coordinatorId,
        string? subject = null,
        string? emailThreadId = null,
        DateTimeOffset? preferredDate = null,
        string? accessNotes = null,
        RequestSource source = RequestSource.Manual,
        string? emailFrom = null,
        string? emailPreview = null,
        bool hasUnreadClientReply = false,
        string? seededBy = null,
        DateTimeOffset? receivedAt = null,
        Guid? managerId = null)
    {
        if (string.IsNullOrWhiteSpace(clientName)) throw new ArgumentException("Client name is required.", nameof(clientName));
        if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("Location is required.", nameof(location));
        if (coordinatorId == Guid.Empty) throw new ArgumentException("Coordinator is required.", nameof(coordinatorId));
        if (clientId == Guid.Empty) throw new ArgumentException("Client id cannot be empty.", nameof(clientId));
        ArgumentNullException.ThrowIfNull(items);
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));

        var requestItems = items.ToList();
        if (requestItems.Count == 0) throw new ArgumentException("At least one item is required.", nameof(items));

        var now = DateTimeOffset.UtcNow;
        var request = new InspectionRequest
        {
            Id = Guid.NewGuid(),
            ManagerId = managerId,
            ClientName = clientName.Trim(),
            ClientId = clientId,
            Location = location.Trim(),
            Subject = subject?.Trim(),
            EmailThreadId = emailThreadId,
            EmailFrom = emailFrom?.Trim(),
            EmailPreview = emailPreview?.Trim(),
            Source = source,
            HasUnreadClientReply = hasUnreadClientReply,
            SeededBy = seededBy?.Trim(),
            ReceivedAt = receivedAt ?? now,
            PreferredDate = preferredDate,
            AccessNotes = accessNotes,
            CoordinatorId = coordinatorId,
            Status = JobStatus.RequestReceived,
            CreatedAt = now,
            UpdatedAt = now
        };
        request.Items.AddRange(requestItems);
        request.History.Add(JobStatusHistory.Create(request.Id, null, JobStatus.RequestReceived, coordinatorId, now, null));
        return request;
    }

    /// <summary>
    /// Creates a request from a mailbox message. Client, location and items start empty and are
    /// completed by the owning coordinator before a quote can be drafted.
    /// </summary>
    public static InspectionRequest CreateFromEmail(
        Guid managerId,
        Guid coordinatorId,
        IncomingEmail email,
        DateTimeOffset? receivedAt = null)
    {
        if (managerId == Guid.Empty) throw new ArgumentException("Manager is required.", nameof(managerId));
        if (coordinatorId == Guid.Empty) throw new ArgumentException("Coordinator is required.", nameof(coordinatorId));
        ArgumentNullException.ThrowIfNull(email);

        var now = DateTimeOffset.UtcNow;
        var received = receivedAt ?? now;
        var subject = string.IsNullOrWhiteSpace(email.Subject) ? null : email.Subject.Trim();
        var request = new InspectionRequest
        {
            Id = Guid.NewGuid(),
            ManagerId = managerId,
            CoordinatorId = coordinatorId,
            Subject = subject is { Length: > 998 } ? subject[..998] : subject,
            EmailThreadId = BuildThreadId(email),
            EmailFrom = string.IsNullOrWhiteSpace(email.FromAddress) ? null : email.FromAddress.Trim(),
            EmailFromName = string.IsNullOrWhiteSpace(email.FromName) ? null : email.FromName.Trim(),
            EmailPreview = RequestEmail.BuildPreview(email.TextBody, email.HtmlBody),
            Source = RequestSource.Email,
            ReceivedAt = email.SentAt ?? received,
            Status = JobStatus.RequestReceived,
            CreatedAt = now,
            UpdatedAt = now
        };
        request.Email = RequestEmail.Create(
            request.Id,
            coordinatorId,
            email.MailboxUidValidity,
            email.MailboxUid,
            email.TextBody,
            email.HtmlBody,
            email.FromAddress,
            email.FromName,
            email.Subject,
            email.To,
            email.Cc,
            email.Bcc,
            email.ReplyTo,
            email.Attachments,
            email.MessageId,
            email.InReplyTo,
            email.References,
            email.SentAt,
            received);
        request.History.Add(JobStatusHistory.Create(
            request.Id, null, JobStatus.RequestReceived, coordinatorId, now, "Received by email"));
        return request;
    }

    /// <summary>Names of the fields that must still be filled in before a quote can be drafted.</summary>
    public IReadOnlyList<string> GetMissingFields()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(ClientName)) missing.Add("clientName");
        if (string.IsNullOrWhiteSpace(Location)) missing.Add("location");
        if (Items.Count == 0) missing.Add("items");
        return missing;
    }

    public bool CanEditDetails => Status is JobStatus.RequestReceived or JobStatus.QuoteDraft;

    public void SetClient(Guid clientId)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("Client is required.", nameof(clientId));
        if (!CanEditDetails)
            throw new InvalidOperationException("Client can only be matched before a quote is sent.");

        ClientId = clientId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDetails(
        string? clientName,
        string? location,
        Guid? clientId,
        DateTimeOffset? preferredDate,
        string? accessNotes)
    {
        EnsureEditable();
        if (clientId == Guid.Empty) throw new ArgumentException("Client id cannot be empty.", nameof(clientId));

        ClientName = clientName?.Trim() ?? string.Empty;
        Location = location?.Trim() ?? string.Empty;
        ClientId = clientId;
        PreferredDate = preferredDate;
        AccessNotes = string.IsNullOrWhiteSpace(accessNotes) ? null : accessNotes.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public RequestItem AddItem(RequestItem item)
    {
        EnsureEditable();
        ArgumentNullException.ThrowIfNull(item);
        Items.Add(item);
        UpdatedAt = DateTimeOffset.UtcNow;
        return item;
    }

    public RequestItem UpdateItem(
        Guid itemId,
        string name,
        int quantity,
        Guid? equipmentId,
        string? type,
        string? capacity,
        string? serialNumber,
        string? location,
        string? issueDescription)
    {
        EnsureEditable();
        var item = FindItem(itemId);
        item.Update(name, quantity, equipmentId, type, capacity, serialNumber, location, issueDescription);
        UpdatedAt = DateTimeOffset.UtcNow;
        return item;
    }

    public RequestItem RemoveItem(Guid itemId)
    {
        EnsureEditable();
        var item = FindItem(itemId);
        Items.Remove(item);
        UpdatedAt = DateTimeOffset.UtcNow;
        return item;
    }

    private RequestItem FindItem(Guid itemId) =>
        Items.SingleOrDefault(item => item.Id == itemId)
            ?? throw new KeyNotFoundException("Request item was not found.");

    private void EnsureEditable()
    {
        if (!CanEditDetails)
            throw new InvalidOperationException(
                $"Request details can only be edited while the request is {JobStatus.RequestReceived} or {JobStatus.QuoteDraft}.");
    }

    private static string? BuildThreadId(IncomingEmail email)
    {
        var root = email.References?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        var threadId = root ?? email.InReplyTo ?? email.MessageId;
        return threadId is { Length: > 256 } ? threadId[..256] : threadId;
    }

    public void AssignInspector(Guid inspectorId, DateTimeOffset scheduledAt, Guid coordinatorId, string? note = null)
    {
        if (coordinatorId == Guid.Empty) throw new ArgumentException("Coordinator is required.", nameof(coordinatorId));
        if (Status != JobStatus.QuoteAccepted)
            throw new InvalidOperationException("Only accepted jobs can be assigned.");
        if (inspectorId == Guid.Empty) throw new ArgumentException("Inspector is required.", nameof(inspectorId));

        InspectorId = inspectorId;
        ScheduledAt = scheduledAt;
        AssignmentNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        TransitionTo(JobStatus.Assigned, coordinatorId, note);
    }

    public void TransitionTo(JobStatus nextStatus, Guid actorId, string? note = null)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorId));
        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(nextStatus))
            throw new InvalidOperationException($"Transition from {Status} to {nextStatus} is not allowed.");

        if (nextStatus == JobStatus.QuoteDraft && GetMissingFields() is { Count: > 0 } missing)
            throw new InvalidOperationException(
                $"Complete the request before drafting a quote. Missing: {string.Join(", ", missing)}.");
        if (nextStatus == JobStatus.QuoteSent && (ClientId is null || Items.Count == 0 || string.IsNullOrWhiteSpace(Location)))
            throw new InvalidOperationException("A matched client, at least one item, and a location are required before quoting.");
        if (nextStatus == JobStatus.CertificateSubmitted && InspectorId is null)
            throw new InvalidOperationException("An assigned inspector is required before certificate submission.");
        if (nextStatus == JobStatus.QuoteRejected && string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException("A rejection reason is required.");

        var previousStatus = Status;
        Status = nextStatus;
        UpdatedAt = DateTimeOffset.UtcNow;
        if (nextStatus == JobStatus.CertificateSubmitted)
            CertificateSubmittedAt = UpdatedAt;
        if (nextStatus == JobStatus.QuoteRejected) RejectReason = note!.Trim();
        History.Add(JobStatusHistory.Create(Id, previousStatus, nextStatus, actorId, UpdatedAt, note));
    }

    public void MarkClientReplyRead()
    {
        HasUnreadClientReply = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordClientReplyReceived()
    {
        HasUnreadClientReply = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}