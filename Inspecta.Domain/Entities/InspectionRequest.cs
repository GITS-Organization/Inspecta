using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

public sealed class InspectionRequest
{
    private static readonly IReadOnlyDictionary<JobStatus, JobStatus[]> AllowedTransitions =
        new Dictionary<JobStatus, JobStatus[]>
        {
            [JobStatus.RequestReceived] = [JobStatus.QuoteDraft, JobStatus.QuoteRejected],
            [JobStatus.QuoteDraft] = [JobStatus.QuoteSent, JobStatus.CounterOfferSent, JobStatus.QuoteRejected],
            [JobStatus.QuoteSent] = [JobStatus.ClientCountered, JobStatus.QuoteRejected, JobStatus.Unassigned, JobStatus.ClientDeclined, JobStatus.QuoteDraft],
            // A counter offer waits for the client like the first offer, and a new revision can follow it.
            [JobStatus.CounterOfferSent] = [JobStatus.ClientCountered, JobStatus.QuoteRejected, JobStatus.Unassigned, JobStatus.ClientDeclined, JobStatus.QuoteDraft],
            // The client answered: the coordinator can revise the offer, accept it, turn it down, or dismiss the reply and keep waiting.
            [JobStatus.ClientCountered] = [JobStatus.QuoteDraft, JobStatus.QuoteRejected, JobStatus.Unassigned, JobStatus.ClientDeclined, JobStatus.QuoteSent, JobStatus.CounterOfferSent],
            [JobStatus.Unassigned] = [JobStatus.Assigned],
            // Handing the job to another inspector keeps it in Assigned (and puts an accepted task back to it).
            [JobStatus.Assigned] = [JobStatus.TaskAccepted, JobStatus.Assigned],
            [JobStatus.TaskAccepted] = [JobStatus.InProgress, JobStatus.Assigned],
            [JobStatus.InProgress] = [JobStatus.CertificateSubmitted],
            [JobStatus.CertificateSubmitted] = [JobStatus.CertificateReturned, JobStatus.CertificateApproved],
            [JobStatus.CertificateReturned] = [JobStatus.InProgress],
            [JobStatus.CertificateApproved] = [JobStatus.SentToClient]
        };

    /// <summary>How long an unconfirmed "maybe" email is kept before it is deleted automatically.</summary>
    public static readonly TimeSpan MaybeRetention = TimeSpan.FromDays(7);

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
    public RequestKind Kind { get; private set; } = RequestKind.Standard;

    /// <summary>How many days of one inspector (a "man day") the quotation also sells, next to the machines; null when none.</summary>
    public int? ManDayDays { get; private set; }

    /// <summary>Asks for a man day line in the quotation (1 to 365 days), or none (null or 0). Allowed while the request can still be edited.</summary>
    public void SetManDay(int? days)
    {
        if (days is < 0 or > 365) throw new ArgumentOutOfRangeException(nameof(days), "A man day request is between 1 and 365 days.");
        var value = days is > 0 ? days : null;
        if (value == ManDayDays) return;
        EnsureEditable();
        ManDayDays = value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
    public RequestTriage Triage { get; private set; } = RequestTriage.Request;
    public bool HasUnreadClientReply { get; private set; }
    public string? SeededBy { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset? PreferredDate { get; private set; }
    public string? AccessNotes { get; private set; }
    public DateTimeOffset? CertificateSubmittedAt { get; private set; }
    public JobStatus Status { get; private set; }
    public Guid? CoordinatorId { get; private set; }
    public Guid? InspectorId { get; private set; }

    /// <summary>
    /// The technical manager who reviews and approves this job's certificates once the inspector submits them. A request starts with the
    /// company's default reviewer; the coordinator can change it until the job is approved.
    /// </summary>
    public Guid? ReviewerId { get; private set; }
    public DateTimeOffset? ScheduledAt { get; private set; }
    public string? AssignmentNote { get; private set; }
    public string? RejectReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Client? Client { get; private set; }
    public List<InspectionCertificate> Certificates { get; private set; } = [];
    public List<RequestItem> Items { get; private set; } = [];
    public List<JobStatusHistory> History { get; private set; } = [];

    /// <summary>The technical manager's comments each time the job was sent back to the inspector.</summary>
    public List<ReviewComment> ReviewComments { get; private set; } = [];
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
    /// A man day: a client asked for an inspector on a day. There are no machines yet and no quotation; the job goes straight to the
    /// inspector, who adds the machines found on site and certifies them.
    /// </summary>
    public static InspectionRequest CreateManDay(
        string clientName,
        string location,
        Guid clientId,
        Guid managerId,
        Guid coordinatorId,
        Guid inspectorId,
        DateTimeOffset scheduledAt,
        string? note = null)
    {
        if (string.IsNullOrWhiteSpace(clientName)) throw new ArgumentException("Client name is required.", nameof(clientName));
        if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("Location is required.", nameof(location));
        if (clientId == Guid.Empty) throw new ArgumentException("Client is required.", nameof(clientId));
        if (managerId == Guid.Empty) throw new ArgumentException("Manager is required.", nameof(managerId));
        if (coordinatorId == Guid.Empty) throw new ArgumentException("Coordinator is required.", nameof(coordinatorId));
        if (inspectorId == Guid.Empty) throw new ArgumentException("Inspector is required.", nameof(inspectorId));

        var now = DateTimeOffset.UtcNow;
        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var request = new InspectionRequest
        {
            Id = Guid.NewGuid(),
            ManagerId = managerId,
            ClientName = clientName.Trim(),
            ClientId = clientId,
            Location = location.Trim(),
            Subject = "Man day",
            Source = RequestSource.Manual,
            Kind = RequestKind.ManDay,
            ReceivedAt = now,
            PreferredDate = scheduledAt,
            AccessNotes = cleanNote,
            CoordinatorId = coordinatorId,
            InspectorId = inspectorId,
            ScheduledAt = scheduledAt,
            AssignmentNote = cleanNote,
            Status = JobStatus.Assigned,
            CreatedAt = now,
            UpdatedAt = now
        };
        request.History.Add(JobStatusHistory.Create(request.Id, null, JobStatus.RequestReceived, coordinatorId, now, "Man day requested."));
        request.History.Add(JobStatusHistory.Create(request.Id, JobStatus.RequestReceived, JobStatus.Assigned, coordinatorId, now, cleanNote));
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
        DateTimeOffset? receivedAt = null,
        RequestTriage triage = RequestTriage.Request,
        Guid? clientId = null,
        string? clientName = null,
        string? location = null)
    {
        if (!Enum.IsDefined(triage)) throw new ArgumentOutOfRangeException(nameof(triage));
        if (clientId == Guid.Empty) throw new ArgumentException("Client id cannot be empty.", nameof(clientId));

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
            Triage = triage,
            ClientId = clientId,
            ClientName = clientName?.Trim() ?? string.Empty,
            Location = location?.Trim() ?? string.Empty,
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

    public bool CanEditDetails =>
        Triage == RequestTriage.Request && Status is JobStatus.RequestReceived or JobStatus.QuoteDraft;

    /// <summary>When an unconfirmed "maybe" email will be deleted automatically.</summary>
    public DateTimeOffset? MaybeExpiresAt => Triage == RequestTriage.Maybe ? CreatedAt + MaybeRetention : null;

    /// <summary>Turns a "maybe" email into a real request.</summary>
    public void Confirm()
    {
        if (Triage != RequestTriage.Maybe)
            throw new InvalidOperationException("Only a maybe email can be confirmed as a request.");

        Triage = RequestTriage.Request;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Turns a "maybe" email into a real request of a client whose sender has just been registered.</summary>
    public void AdoptClient(Guid clientId, string clientName, string? location = null)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("Client is required.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(clientName)) throw new ArgumentException("Client name is required.", nameof(clientName));

        Confirm();
        ClientId = clientId;
        ClientName = clientName.Trim();
        if (string.IsNullOrWhiteSpace(Location) && !string.IsNullOrWhiteSpace(location))
            Location = location.Trim();
    }

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

    /// <summary>
    /// Saves the details and, when <paramref name="items"/> is given, replaces the item list in one step: items with an
    /// <c>Id</c> of this request are updated, items without one (or with an id the request does not know) are added, and existing items that are not listed are removed. Everything
    /// is validated before anything changes, so a rejected revision leaves the request untouched.
    /// </summary>
    public RequestItemChanges Revise(
        string? clientName,
        string? location,
        Guid? clientId,
        DateTimeOffset? preferredDate,
        string? accessNotes,
        IReadOnlyList<RequestItemSpec>? items)
    {
        EnsureEditable();
        if (clientId == Guid.Empty) throw new ArgumentException("Client id cannot be empty.", nameof(clientId));

        List<RequestItem> added = [];
        List<RequestItem> removed = [];
        if (items is not null)
        {
            var ids = items.Where(spec => spec.Id is not null).Select(spec => spec.Id!.Value).ToList();
            if (ids.Any(id => id == Guid.Empty))
                throw new ArgumentException("Item id cannot be empty.");
            var repeated = ids.GroupBy(id => id).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
            if (repeated.Count > 0)
                throw new ArgumentException($"Each item can only appear once: {string.Join(", ", repeated)}.");
            foreach (var spec in items)
                RequestItem.Validate(spec.Name, spec.Quantity, spec.EquipmentId);

            // An id this request does not know is an item the screen created itself (screens that work offline, or
            // that number their rows, send their own ids): it is added with that id.
            var known = Items.Select(item => item.Id).ToHashSet();
            foreach (var spec in items.Where(spec => spec.Id is null || !known.Contains(spec.Id.Value)))
                added.Add(RequestItem.Create(
                    spec.Name, spec.Quantity, spec.EquipmentId, spec.Type, spec.Capacity, spec.SerialNumber,
                    location: spec.Location, issueDescription: spec.IssueDescription, id: spec.Id));
            foreach (var spec in items.Where(spec => spec.Id is not null && known.Contains(spec.Id.Value)))
                Items.Single(item => item.Id == spec.Id).Update(
                    spec.Name, spec.Quantity, spec.EquipmentId, spec.Type, spec.Capacity, spec.SerialNumber,
                    spec.Location, spec.IssueDescription);
            removed.AddRange(Items.Where(item => ids.All(id => id != item.Id)));
            foreach (var item in removed) Items.Remove(item);
            Items.AddRange(added);
        }

        ClientName = clientName?.Trim() ?? string.Empty;
        Location = location?.Trim() ?? string.Empty;
        ClientId = clientId;
        PreferredDate = preferredDate;
        AccessNotes = string.IsNullOrWhiteSpace(accessNotes) ? null : accessNotes.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
        return new RequestItemChanges(added, removed);
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

    /// <summary>True while machines can still be added, changed or removed: from the assignment until the job is submitted.</summary>
    public bool CanChangeMachines =>
        Status is JobStatus.Assigned or JobStatus.TaskAccepted or JobStatus.InProgress or JobStatus.CertificateReturned;

    private void EnsureMachinesCanChange()
    {
        if (!CanChangeMachines)
            throw new InvalidOperationException("Machines can be changed from the assignment until the inspector submits the job.");
    }

    /// <summary>Adds a machine to a job that is under way (the inspector found one on site, or the coordinator added one).</summary>
    public RequestItem AddMachine(RequestItem item, Guid actorId)
    {
        EnsureMachinesCanChange();
        ArgumentNullException.ThrowIfNull(item);
        if (item.EquipmentId is Guid equipmentId && Items.Any(value => value.EquipmentId == equipmentId))
            throw new InvalidOperationException("This machine is already in the job.");

        Items.Add(item);
        UpdatedAt = DateTimeOffset.UtcNow;
        History.Add(JobStatusHistory.Create(Id, Status, Status, actorId, UpdatedAt, $"Machine added: {item.Name}."));
        return item;
    }

    /// <summary>
    /// Divides a gear into the certificates the inspector decided on: every share is one certificate with its own quantity and its own
    /// serial text. The given item keeps the first share, one copy is added for each other share, and the certificates of an earlier
    /// division of the same gear are replaced. There is no limit on the number of shares.
    /// </summary>
    public (IReadOnlyList<RequestItem> Added, IReadOnlyList<RequestItem> Removed) DivideItem(
        Guid itemId, IReadOnlyList<(int Quantity, string Serials)> shares, Guid actorId)
    {
        EnsureMachinesCanChange();
        if (shares.Count < 1) throw new ArgumentException("At least one certificate is needed.", nameof(shares));

        var item = FindItem(itemId);
        var groupId = item.DivisionGroupId ?? Guid.NewGuid();
        var total = item.DivisionTotal ?? item.Quantity;
        var removed = item.DivisionGroupId is null
            ? new List<RequestItem>()
            : Items.Where(value => value.DivisionGroupId == item.DivisionGroupId && value.Id != item.Id).ToList();
        foreach (var gone in removed) Items.Remove(gone);

        item.SetShare(shares[0].Quantity, shares[0].Serials);
        item.JoinDivision(groupId, total);
        var added = new List<RequestItem>();
        for (var index = 1; index < shares.Count; index++)
        {
            var copy = item.CopyWith(shares[index].Quantity, shares[index].Serials);
            copy.JoinDivision(groupId, total);
            Items.Add(copy);
            added.Add(copy);
        }

        UpdatedAt = DateTimeOffset.UtcNow;
        History.Add(JobStatusHistory.Create(Id, Status, Status, actorId, UpdatedAt, $"{item.Name} was divided into {shares.Count} certificates."));
        return (added, removed);
    }

    /// <summary>Changes the details of one machine of a job that is under way.</summary>
    public RequestItem EditMachine(
        Guid itemId, string name, int quantity, string? type, string? capacity, string? serialNumber,
        string? manufacturer, string? model, string? location, string? issueDescription, Guid actorId)
    {
        EnsureMachinesCanChange();
        var item = FindItem(itemId);
        item.Edit(name, quantity, type, capacity, serialNumber, manufacturer, model, location, issueDescription);
        UpdatedAt = DateTimeOffset.UtcNow;
        History.Add(JobStatusHistory.Create(Id, Status, Status, actorId, UpdatedAt, $"Machine changed: {item.Name}."));
        return item;
    }

    /// <summary>Takes a machine out of a job that is under way. A standard job keeps at least one machine.</summary>
    public RequestItem RemoveMachine(Guid itemId, Guid actorId)
    {
        EnsureMachinesCanChange();
        var item = FindItem(itemId);
        if (Kind != RequestKind.ManDay && Items.Count == 1)
            throw new InvalidOperationException("The job needs at least one machine.");

        Items.Remove(item);
        UpdatedAt = DateTimeOffset.UtcNow;
        History.Add(JobStatusHistory.Create(Id, Status, Status, actorId, UpdatedAt, $"Machine removed: {item.Name}."));
        return item;
    }

    /// <summary>
    /// True when a client with a contract asked for work and nobody has priced it: the coordinator may give it to an inspector straight away
    /// instead of sending a quotation.
    /// </summary>
    public bool CanAssignWithoutQuotation =>
        Status == JobStatus.RequestReceived && Triage == RequestTriage.Request && Kind == RequestKind.Standard &&
        ClientId is not null && Client is { HasContract: true } && !string.IsNullOrWhiteSpace(Location);

    /// <summary>
    /// Gives a request of a contract client to an inspector without a quotation: it goes from <c>request_received</c> straight to
    /// <c>assigned</c>. The items stay as they are; the inspector may add machines found on site.
    /// </summary>
    public void AssignWithoutQuotation(Guid inspectorId, DateTimeOffset scheduledAt, Guid actorId, string? note = null)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorId));
        if (inspectorId == Guid.Empty) throw new ArgumentException("Inspector is required.", nameof(inspectorId));
        if (!CanAssignWithoutQuotation)
            throw new InvalidOperationException("Only a new request of a client with a contract, with its client and location filled in, can go to an inspector without a quotation.");

        var previous = Status;
        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Kind = RequestKind.Contract;
        InspectorId = inspectorId;
        ScheduledAt = scheduledAt;
        AssignmentNote = cleanNote;
        Status = JobStatus.Assigned;
        UpdatedAt = DateTimeOffset.UtcNow;
        History.Add(JobStatusHistory.Create(Id, previous, Status, actorId, UpdatedAt, cleanNote ?? "Client with a contract: given to an inspector without a quotation."));
    }

    /// <summary>
    /// Starts the inspection of a request that never had a quotation: a certificate created directly by the person who inspects.
    /// The job goes from <c>request_received</c> straight to <c>in_progress</c> with that person as its inspector.
    /// </summary>
    public void BeginDirectInspection(Guid actorId)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorId));
        if (Status != JobStatus.RequestReceived || Items.Count == 0)
            throw new InvalidOperationException("Only a new request with at least one machine can start directly.");

        var previous = Status;
        Kind = RequestKind.Direct;
        InspectorId = actorId;
        Status = JobStatus.InProgress;
        UpdatedAt = DateTimeOffset.UtcNow;
        History.Add(JobStatusHistory.Create(Id, previous, Status, actorId, UpdatedAt, "Certificate created directly, without a quotation."));
    }

    /// <summary>
    /// Divides one machine of the job over several certificates: the item keeps the first share and one copy is added for each other share.
    /// Allowed while the inspector fills the certificates.
    /// </summary>
    public IReadOnlyList<RequestItem> SplitItem(Guid itemId, IReadOnlyList<IReadOnlyList<string>> shares, string separator, Guid actorId)
    {
        if (Status is not (JobStatus.InProgress or JobStatus.CertificateReturned))
            throw new InvalidOperationException("A machine can be split while the inspection is in progress.");
        if (shares.Count < 1 || shares.Any(share => share.Count == 0))
            throw new ArgumentException("Every certificate needs at least one serial number.", nameof(shares));

        var item = FindItem(itemId);
        var added = new List<RequestItem>();
        for (var index = 1; index < shares.Count; index++)
        {
            var copy = item.CopyWith(shares[index].Count, string.Join(separator, shares[index]));
            Items.Add(copy);
            added.Add(copy);
        }

        item.SetShare(shares[0].Count, string.Join(separator, shares[0]));
        UpdatedAt = DateTimeOffset.UtcNow;
        History.Add(JobStatusHistory.Create(Id, Status, Status, actorId, UpdatedAt, $"{item.Name} was divided into {shares.Count} certificates."));
        return added;
    }

    public void AssignInspector(Guid inspectorId, DateTimeOffset scheduledAt, Guid coordinatorId, string? note = null)
    {
        if (coordinatorId == Guid.Empty) throw new ArgumentException("Coordinator is required.", nameof(coordinatorId));
        if (!CanAssign)
            throw new InvalidOperationException(
                $"A job can be given to an inspector while it is {JobStatus.Unassigned}, {JobStatus.Assigned} or {JobStatus.TaskAccepted}.");
        if (inspectorId == Guid.Empty) throw new ArgumentException("Inspector is required.", nameof(inspectorId));

        InspectorId = inspectorId;
        ScheduledAt = scheduledAt;
        AssignmentNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        TransitionTo(JobStatus.Assigned, coordinatorId, note);
    }

    /// <summary>True while the job has not started: it waits for an inspector, or its inspector can still be replaced.</summary>
    public bool CanAssign => Status is JobStatus.Unassigned or JobStatus.Assigned or JobStatus.TaskAccepted;

    /// <summary>Moves the inspection to another date. Allowed until the inspector starts the work.</summary>
    public void ChangeInspectionDate(DateTimeOffset scheduledAt, Guid actorId, string? note = null)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorId));
        if (InspectorId is null || Status is not (JobStatus.Assigned or JobStatus.TaskAccepted))
            throw new InvalidOperationException(
                $"The inspection date can be changed while the job is {JobStatus.Assigned} or {JobStatus.TaskAccepted}.");

        var previous = ScheduledAt;
        ScheduledAt = scheduledAt;
        UpdatedAt = DateTimeOffset.UtcNow;
        var change = $"Inspection date changed from {previous:u} to {scheduledAt:u}.";
        History.Add(JobStatusHistory.Create(
            Id, Status, Status, actorId, UpdatedAt, string.IsNullOrWhiteSpace(note) ? change : $"{change} {note.Trim()}"));
    }

    /// <summary>Starts the note of the history entry that records a change of reviewer; the timeline recognises it by this.</summary>
    public const string ReviewerNotePrefix = "Reviewer changed from ";

    /// <summary>The reviewer a new request starts with (the company default). No history entry: nothing was changed yet.</summary>
    public void UseReviewer(Guid? reviewerId)
    {
        if (reviewerId == Guid.Empty) throw new ArgumentException("Reviewer id cannot be empty.", nameof(reviewerId));
        ReviewerId = reviewerId;
    }

    /// <summary>
    /// Gives the job to another reviewer. Allowed until the job is approved (the certificates are then final). The history gets one entry
    /// (from and to the same status, so an event and not a stage) naming both. Whether the new reviewer is a technical manager of the
    /// company is the service's job.
    /// </summary>
    public void ChangeReviewer(Guid newReviewerId, Guid actorId, string fromName, string toName, string? note = null)
    {
        if (newReviewerId == Guid.Empty) throw new ArgumentException("The new reviewer is required.", nameof(newReviewerId));
        if (actorId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorId));
        if (newReviewerId == ReviewerId) throw new InvalidOperationException("The job already has this reviewer.");
        if (Triage == RequestTriage.Maybe) throw new InvalidOperationException("Confirm the email as a request before choosing its reviewer.");
        if (Status is JobStatus.CertificateApproved or JobStatus.SentToClient or JobStatus.QuoteRejected or JobStatus.ClientDeclined)
            throw new InvalidOperationException("The reviewer cannot change once the certificates are approved or the request is finished.");

        ReviewerId = newReviewerId;
        UpdatedAt = DateTimeOffset.UtcNow;
        var text = $"{ReviewerNotePrefix}{fromName} to {toName}.";
        History.Add(JobStatusHistory.Create(
            Id, Status, Status, actorId, UpdatedAt, string.IsNullOrWhiteSpace(note) ? text : $"{text} {note.Trim()}"));
    }

    /// <summary>Starts the note of the history entry that records a transfer; the timeline recognises it by this.</summary>
    public const string TransferNotePrefix = "Transferred from ";

    /// <summary>
    /// Hands the request to another coordinator of the team. The status does not change; the history gets one entry (from and to the same
    /// status, so it is an event and not a stage) that names both coordinators. Whether the caller may do it, and whether the target is a
    /// coordinator of the same team, is the service's job: this only guards the request itself.
    /// </summary>
    public void TransferTo(Guid newCoordinatorId, Guid actorId, string fromName, string toName, string? note = null)
    {
        if (newCoordinatorId == Guid.Empty) throw new ArgumentException("The new coordinator is required.", nameof(newCoordinatorId));
        if (actorId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorId));
        if (newCoordinatorId == CoordinatorId)
            throw new InvalidOperationException("The request already belongs to this coordinator.");
        if (Triage == RequestTriage.Maybe)
            throw new InvalidOperationException("Confirm the email as a request before transferring it.");
        if (Status is JobStatus.SentToClient or JobStatus.QuoteRejected or JobStatus.ClientDeclined)
            throw new InvalidOperationException("A finished request cannot be transferred.");

        CoordinatorId = newCoordinatorId;
        UpdatedAt = DateTimeOffset.UtcNow;
        var text = $"{TransferNotePrefix}{fromName} to {toName}.";
        History.Add(JobStatusHistory.Create(
            Id, Status, Status, actorId, UpdatedAt, string.IsNullOrWhiteSpace(note) ? text : $"{text} {note.Trim()}"));
    }

    public void TransitionTo(JobStatus nextStatus, Guid actorId, string? note = null)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Actor is required.", nameof(actorId));
        if (Triage == RequestTriage.Maybe)
            throw new InvalidOperationException("Confirm the email as a request before changing its status.");
        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(nextStatus))
            throw new InvalidOperationException($"Transition from {Status} to {nextStatus} is not allowed.");

        if (nextStatus == JobStatus.QuoteDraft && GetMissingFields() is { Count: > 0 } missing)
            throw new InvalidOperationException(
                $"Complete the request before drafting a quote. Missing: {string.Join(", ", missing)}.");
        if (nextStatus is JobStatus.QuoteSent or JobStatus.CounterOfferSent && (Items.Count == 0 || string.IsNullOrWhiteSpace(Location)))
            throw new InvalidOperationException("At least one item and a location are required before quoting.");
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

    /// <summary>
    /// The client answered a quotation by email. A request that was waiting for the client goes to <c>client_countered</c>, so it is back in front
    /// of the coordinator; in any other stage the reply is only flagged as unread.
    /// </summary>
    public void ReceiveClientReply(Guid actorId)
    {
        if (Status is JobStatus.QuoteSent or JobStatus.CounterOfferSent)
            TransitionTo(JobStatus.ClientCountered, actorId, "The client replied by email.");
        RecordClientReplyReceived();
    }

    /// <summary>
    /// The reply needs nothing from the coordinator: a request that was waiting on it goes back to waiting for the client (<paramref name="waitingStatus"/>,
    /// <c>quote_sent</c> or <c>counter_offer_sent</c>), and the reply is no longer unread.
    /// </summary>
    public void DismissClientReply(Guid actorId, JobStatus waitingStatus)
    {
        if (waitingStatus is not (JobStatus.QuoteSent or JobStatus.CounterOfferSent))
            throw new ArgumentOutOfRangeException(nameof(waitingStatus));
        if (Status == JobStatus.ClientCountered)
            TransitionTo(waitingStatus, actorId, "The client's reply was dismissed.");
        MarkClientReplyRead();
    }

    public void RecordClientReplyReceived()
    {
        HasUnreadClientReply = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}