using Inspecta.Application.Contracts;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class InspectionRequestService(
    IRequestRepository requestRepository,
    IUserRepository userRepository,
    IRequestEquipmentRegistrar? equipmentRegistrar = null,
    IEmailSyncRepository? senderRules = null,
    IMachineSuggestionService? suggestions = null,
    IClientRepository? clientRepository = null,
    IQuoteRepository? quoteRepository = null,
    ITransactionRunner? transactions = null,
    ICompanyGate? companyGate = null,
    IReviewerService? reviewers = null) : IInspectionRequestService
{
    public async Task<RequestResponse> CreateAsync(
        CreateInspectionRequest request,
        Guid coordinatorId,
        Guid? managerId,
        CancellationToken cancellationToken = default)
        => await CreateCoreAsync(request, coordinatorId, managerId, RequestSource.Manual, null, null, cancellationToken);

    public async Task<RequestResponse> CreateManDayAsync(
        CreateManDayRequest request, Guid actorId, string role, Guid? managerId, CancellationToken cancellationToken = default)
    {
        if (role is not ("admin" or "technical_manager" or "coordinator"))
            throw new AuthException("forbidden", "Only the coordinator, the technical manager or the admin can request a man day.", 403);
        if (clientRepository is null) throw new InvalidOperationException("Clients are not available.");

        var client = await clientRepository.GetByIdAsync(request.ClientId, tracking: false, cancellationToken)
            ?? throw new KeyNotFoundException("Client was not found.");
        if (client.ArchivedAt is not null) throw new KeyNotFoundException("Client was not found.");
        var teamId = (role == "admin" ? request.ManagerId ?? client.ManagerId : managerId)
            ?? throw new ArgumentException("The client does not belong to a team.");
        if (client.ManagerId != teamId)
            throw new AuthException("forbidden", "The client does not belong to the selected team.", 403);

        var inspector = await userRepository.GetByIdAsync(request.InspectorId, cancellationToken);
        if (inspector is null || !inspector.IsActive || inspector.Role != UserRole.Inspector)
            throw new InvalidOperationException("The selected inspector is unavailable.");
        if (inspector.ManagerId != teamId)
            throw new AuthException("forbidden", "The selected inspector does not belong to the client's team.", 403);
        if (inspector.IsOnLeave && (inspector.LeaveUntil is null || inspector.LeaveUntil >= request.ScheduledAt))
            throw new InvalidOperationException("The selected inspector is on leave at the scheduled time.");

        var manDay = InspectionRequest.CreateManDay(
            client.Name,
            string.IsNullOrWhiteSpace(request.Location) ? client.Location ?? client.Name : request.Location,
            client.Id,
            teamId,
            actorId,
            inspector.Id,
            request.ScheduledAt,
            request.Note);
        await UseReviewerAsync(manDay, teamId, request.ReviewerId, cancellationToken);
        if (reviewers is not null && manDay.ReviewerId is null)
            throw new AuthException("no_reviewer", "The company has no active technical manager to review the certificates. Create one first.", 409);
        async Task<bool> Save()
        {
            await requestRepository.AddAsync(manDay, cancellationToken);
            // The placeholder quotation puts the job in the quotation queue, where the inspector and the coordinator find it.
            if (quoteRepository is not null)
                await quoteRepository.AddAsync(Quote.CreateAccepted(manDay.Id, actorId, "MD", client.Name, "Man day"), cancellationToken);
            return true;
        }

        if (transactions is null) await Save();
        else await transactions.RunAsync(Save, cancellationToken);
        var created = await requestRepository.GetByIdAsync(manDay.Id, tracking: false, cancellationToken) ?? manDay;
        return RequestResponseMapper.Map(created, await ReviewerNameAsync(created, cancellationToken));
    }

    public async Task<RequestResponse> AssignWithoutQuotationAsync(
        Guid requestId, Guid actorId, string role, AssignWithoutQuotationRequest request, CancellationToken cancellationToken = default)
    {
        if (role is not ("admin" or "technical_manager" or "coordinator"))
            throw new AuthException("forbidden", "Only the coordinator, the technical manager or the admin can assign a request.", 403);

        var job = await requestRepository.GetByIdAsync(requestId, tracking: true, cancellationToken);
        if (job is null || !RequestAccess.IsVisible(job, actorId, role)) throw new KeyNotFoundException("Request was not found.");
        if (role == "coordinator" && !RequestAccess.CanManageQuotes(job, actorId, role))
            throw new AuthException("forbidden", "Only the coordinator that owns the request can assign it.", 403);
        if (job.Client is not { HasContract: true })
            throw new AuthException("no_contract", "Only a client with a contract can be given to an inspector without a quotation.", 409);
        if (!job.CanAssignWithoutQuotation)
            throw new AuthException("not_assignable", "Fill in the client and the location first; a request that already has a quotation or was assigned cannot go this way.", 409);

        var inspector = await userRepository.GetByIdAsync(request.InspectorId, cancellationToken);
        if (inspector is null || !inspector.IsActive || inspector.Role != UserRole.Inspector)
            throw new InvalidOperationException("The selected inspector is unavailable.");
        if (inspector.ManagerId != job.ManagerId)
            throw new AuthException("forbidden", "The selected inspector does not belong to the request team.", 403);
        if (inspector.IsOnLeave && (inspector.LeaveUntil is null || inspector.LeaveUntil >= request.ScheduledAt))
            throw new InvalidOperationException("The selected inspector is on leave at the scheduled time.");

        await ApplyReviewerAsync(job, request.ReviewerId, actorId, role, cancellationToken);
        job.AssignWithoutQuotation(inspector.Id, request.ScheduledAt, actorId, request.Note);
        async Task<bool> Save()
        {
            await requestRepository.SaveAsync(job, cancellationToken);
            // The placeholder quotation (price 0, number CT-...) puts the job in the quotation queue, where the inspector finds it.
            if (quoteRepository is not null)
                await quoteRepository.AddAsync(Quote.CreateAccepted(job.Id, actorId, "CT", job.ClientName, "Client with a contract"), cancellationToken);
            return true;
        }

        if (transactions is null) await Save();
        else await transactions.RunAsync(Save, cancellationToken);
        return RequestResponseMapper.Map(job, await ReviewerNameAsync(job, cancellationToken));
    }

    public async Task<RequestResponse> CreateFromEmailAsync(
        CreateInspectionRequest request,
        string senderEmail,
        string emailBody,
        Guid coordinatorId,
        Guid? managerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(senderEmail))
            throw new ArgumentException("Sender email is required.", nameof(senderEmail));
        if (string.IsNullOrWhiteSpace(emailBody))
            throw new ArgumentException("Email body is required.", nameof(emailBody));
        return await CreateCoreAsync(request, coordinatorId, managerId, RequestSource.Email, senderEmail, emailBody, cancellationToken);
    }

    private async Task<RequestResponse> CreateCoreAsync(
        CreateInspectionRequest request,
        Guid coordinatorId,
        Guid? managerId,
        RequestSource source,
        string? emailFrom,
        string? emailPreview,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0) throw new ArgumentException("At least one request item is required.");
        if (request.ClientId is Guid clientId)
        {
            if (!await requestRepository.ClientExistsAsync(clientId, cancellationToken))
                throw new KeyNotFoundException("Client was not found.");

            var clientManagerId = await requestRepository.GetClientManagerIdAsync(clientId, cancellationToken);
            if (managerId is null && clientManagerId is not null)
                managerId = clientManagerId;
            else if (managerId != clientManagerId)
                throw new AuthException(
                    "forbidden",
                    "The client does not belong to the selected team.",
                    403);
        }

        if (managerId is Guid managerIdValue)
        {
            if (companyGate is not null && !await companyGate.IsActiveAsync(managerIdValue, cancellationToken))
                throw new ArgumentException("The selected company is unavailable.", nameof(managerId));
        }

        var aggregate = InspectionRequest.Create(
            request.ClientName,
            request.Location,
            request.ClientId,
            request.Items.Select(CreateItem),
            coordinatorId,
            subject: request.Subject,
            emailThreadId: request.EmailThreadId,
            preferredDate: request.PreferredDate,
            accessNotes: request.AccessNotes,
            source: source,
            emailFrom: emailFrom,
            emailPreview: emailPreview,
            managerId: managerId);
        if (managerId is Guid companyForReviewer)
            await UseReviewerAsync(aggregate, companyForReviewer, request.ReviewerId, cancellationToken);
        await requestRepository.AddAsync(aggregate, cancellationToken);
        var saved = await requestRepository.GetByIdAsync(aggregate.Id, tracking: false, cancellationToken) ?? aggregate;
        return RequestResponseMapper.Map(saved, await ReviewerNameAsync(saved, cancellationToken));
    }

    public async Task<IReadOnlyList<RequestResponse>> ListVisibleAsync(
        Guid actorId,
        string role,
        RequestListQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, RequestListQuery.MaxPageSize);
        var page = Math.Max(query.Page, 1);
        var (receivedFrom, receivedUntil) = DateRange.Bounds(query.From, query.To);
        var filter = ScopeFor(actorId, role, query.ManagerId) with
        {
            ReceivedFrom = receivedFrom,
            ReceivedUntil = receivedUntil,
            Triage = query.Triage ?? RequestTriage.Request,
            Status = query.Status,
            ExcludeStatuses = query.Status is null ? InQuotation : null,
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Skip = (page - 1) * pageSize,
            Take = pageSize
        };
        var found = await requestRepository.ListAsync(filter, cancellationToken);
        var names = await ReviewerNamesAsync(found, cancellationToken);
        return found.Select(item => RequestResponseMapper.Map(item, ReviewerNameOf(item, names))).ToArray();
    }

    public async Task<RequestSummaryResponse> GetSummaryAsync(
        Guid actorId,
        string role,
        Guid? managerId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var (receivedFrom, receivedUntil) = DateRange.Bounds(from, to);
        var counts = await requestRepository.CountAsync(
            ScopeFor(actorId, role, managerId) with { ReceivedFrom = receivedFrom, ReceivedUntil = receivedUntil }, cancellationToken);
        return new RequestSummaryResponse(
            counts.Where(item => item.Triage == RequestTriage.Request && !InQuotation.Contains(item.Status)).Sum(item => item.Count),
            counts.Where(item => item.Triage == RequestTriage.Maybe).Sum(item => item.Count),
            counts.Where(item => item.Triage == RequestTriage.Request)
                .GroupBy(item => item.Status)
                .Select(group => new RequestStatusCount(group.Key, group.Sum(item => item.Count)))
                .OrderBy(item => item.Status)
                .ToArray());
    }

    public async Task<RequestResponse> ConfirmAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadForEditAsync(requestId, actorId, role, cancellationToken);
        IReadOnlyList<RequestItem> autofilled = [];
        if (senderRules is not null && request.Triage == RequestTriage.Maybe)
        {
            // A maybe becomes a request once its sender belongs to a client: register the sender (or a contact email)
            // under the client first, then the request takes that client.
            var senders = request.ManagerId is Guid managerId
                ? await senderRules.ListSenderRulesAsync(managerId, cancellationToken)
                : [];
            var match = ClientMailRule.FindMatch(senders.Select(item => item.Rule), request.EmailFrom);
            var sender = match is null ? null : senders.First(item => item.Rule.Id == match.Id);
            if (sender?.Rule.ClientId is not Guid clientId || string.IsNullOrWhiteSpace(sender.ClientName))
                throw new AuthException(
                    "client_required",
                    "This sender is not registered under a client yet. Add the sender's email to the client first (POST /clients/{clientId}/emails); the request then moves by itself.",
                    409);
            request.AdoptClient(clientId, sender.ClientName, sender.ClientLocation);
            autofilled = await AutofillQuietlyAsync(request, cancellationToken);
        }
        else
        {
            request.Confirm();
        }

        if (autofilled.Count > 0)
            await requestRepository.SaveRevisionAsync(request, new RequestItemChanges(autofilled, []), cancellationToken);
        else
            await requestRepository.SaveAsync(request, cancellationToken);

        // A maybe may just have taken its client, so the client's data is read again.
        return RequestResponseMapper.Map(await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken) ?? request);
    }

    public async Task<IReadOnlyList<MachineSuggestionResponse>> SuggestMachinesAsync(
        Guid requestId,
        Guid actorId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role))
            throw new KeyNotFoundException("Request was not found.");
        if (suggestions is null) return [];

        var email = await requestRepository.GetEmailAsync(requestId, cancellationToken);
        return (await suggestions.FindAsync(request, email?.TextBody, email?.HtmlBody, cancellationToken))
            .Select(match => new MachineSuggestionResponse(
                match.Machine.EquipmentId,
                match.Machine.Name,
                match.Quantity,
                Math.Round(match.Confidence, 2),
                match.Ambiguous ? "ambiguous" : match.IsConfident ? "high" : "medium",
                match.Reason.ToString().ToLowerInvariant(),
                match.Evidence))
            .ToArray();
    }

    public async Task<RequestResponse> AutofillAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadForEditAsync(requestId, actorId, role, cancellationToken);
        var added = await AutofillQuietlyAsync(request, cancellationToken);
        if (added.Count > 0)
            await requestRepository.SaveRevisionAsync(request, new RequestItemChanges(added, []), cancellationToken);
        return RequestResponseMapper.Map(request);
    }

    // Reading the email is a help: when it cannot be read the request is simply left as it is.
    private async Task<IReadOnlyList<RequestItem>> AutofillQuietlyAsync(InspectionRequest request, CancellationToken cancellationToken)
    {
        if (suggestions is null) return [];
        try
        {
            var email = await requestRepository.GetEmailAsync(request.Id, cancellationToken);
            return await suggestions.AutofillAsync(request, email?.TextBody, email?.HtmlBody, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return [];
        }
    }

    public async Task DeleteMaybeAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadForEditAsync(requestId, actorId, role, cancellationToken);
        if (request.Triage != RequestTriage.Maybe)
            throw new InvalidOperationException("Only a maybe email can be deleted.");
        await requestRepository.DeleteAsync(request, cancellationToken);
    }

    // Once a quotation is open the work continues in the quotation list, so these requests leave the default request list.
    // They stay reachable with an explicit status filter.
    private static readonly JobStatus[] InQuotation = [JobStatus.QuoteDraft, JobStatus.QuoteSent, JobStatus.CounterOfferSent, JobStatus.ClientCountered];

    private static RequestListFilter ScopeFor(Guid actorId, string role, Guid? managerId) => new(
        InspectorId: IsInspector(role) ? actorId : null,
        CoordinatorId: role == "coordinator" ? actorId : null,
        ManagerId: role == "admin" ? managerId : null);

    public async Task<RequestResponse?> GetVisibleAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role)) return null;
        var names = await ReviewerNamesAsync([request], cancellationToken);
        return RequestResponseMapper.Map(request, ReviewerNameOf(request, names));
    }

    public async Task<RequestEmailResponse?> GetEmailAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role)) return null;

        var email = await requestRepository.GetEmailAsync(requestId, cancellationToken);
        return email is null ? null : RequestResponseMapper.MapEmail(email);
    }

    public async Task<RequestResponse> UpdateDetailsAsync(
        Guid requestId,
        Guid actorId,
        string role,
        UpdateRequestDetailsRequest details,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadForEditAsync(requestId, actorId, role, cancellationToken);
        var changes = await new RequestDetailsEditor(requestRepository, equipmentRegistrar).ApplyAsync(request, details, cancellationToken);
        await requestRepository.SaveRevisionAsync(request, changes, cancellationToken);

        // The client may have just been changed, so its data is read again.
        return RequestResponseMapper.Map(await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken) ?? request);
    }

    public async Task<RequestResponse> AddItemAsync(
        Guid requestId,
        Guid actorId,
        string role,
        CreateRequestItem item,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadForEditAsync(requestId, actorId, role, cancellationToken);
        var created = request.AddItem(CreateItem(item));
        await RegisterEquipmentAsync(request, cancellationToken);
        await requestRepository.AddItemAsync(request, created, cancellationToken);
        return RequestResponseMapper.Map(request);
    }

    public async Task<RequestResponse> UpdateItemAsync(
        Guid requestId,
        Guid itemId,
        Guid actorId,
        string role,
        CreateRequestItem item,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadForEditAsync(requestId, actorId, role, cancellationToken);
        request.UpdateItem(
            itemId, item.Name, Certificates.GearRules.QuantityFor(item.Name, item.Quantity), item.EquipmentId, item.Type, item.Capacity,
            item.SerialNumber, item.Location, item.IssueDescription);
        await RegisterEquipmentAsync(request, cancellationToken);
        await requestRepository.SaveAsync(request, cancellationToken);
        return RequestResponseMapper.Map(request);
    }

    public async Task<RequestResponse> RemoveItemAsync(
        Guid requestId,
        Guid itemId,
        Guid actorId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadForEditAsync(requestId, actorId, role, cancellationToken);
        var removed = request.RemoveItem(itemId);
        await requestRepository.RemoveItemAsync(request, removed, cancellationToken);
        return RequestResponseMapper.Map(request);
    }

    public async Task<RequestResponse> TransitionAsync(Guid requestId, Guid actorId, string role, TransitionRequest transition, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: true, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role))
            throw new KeyNotFoundException("Request was not found.");
        AuthorizeTransition(request, actorId, role, transition.Status);
        request.TransitionTo(transition.Status, actorId, transition.Note);
        await requestRepository.SaveAsync(request, cancellationToken);
        return RequestResponseMapper.Map(request);
    }

    public async Task<IReadOnlyList<InspectorResponse>> ListInspectorsAsync(
        Guid actorId,
        string role,
        Guid? managerId = null,
        CancellationToken cancellationToken = default)
    {
        // A coordinator sees the inspectors of their own team, a manager their own, Admin any team (or all).
        Guid? team = role switch
        {
            "technical_manager" or "coordinator" => (await userRepository.GetByIdAsync(actorId, cancellationToken))?.CompanyId,
            _ => managerId
        };
        if (role is "coordinator" or "technical_manager" && team is null) return [];

        var inspectors = await userRepository.ListAsync(team, UserRole.Inspector, isActive: true, cancellationToken);
        var active = await requestRepository.CountActiveByInspectorAsync(
            inspectors.Select(inspector => inspector.Id).ToArray(), cancellationToken);
        return inspectors
            .OrderBy(inspector => inspector.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(inspector => new InspectorResponse(
                inspector.Id,
                inspector.FullName,
                inspector.Email,
                inspector.Qualifications.Select(qualification => qualification.Name).ToArray(),
                inspector.IsOnLeave,
                inspector.LeaveUntil,
                active.GetValueOrDefault(inspector.Id)))
            .ToArray();
    }

    /// <summary>
    /// Gives an accepted job to one inspector. A coordinator may assign what they can manage (their own email requests
    /// and the team's manual ones); managers and Admin may assign any request they can see.
    /// </summary>
    public async Task<RequestResponse> AssignAsync(Guid requestId, Guid actorId, string role, Guid inspectorId, DateTimeOffset scheduledAt, string? note, CancellationToken cancellationToken = default, Guid? reviewerId = null)
    {
        if (role == "inspector")
            throw new AuthException("forbidden", "Inspectors cannot assign jobs.", 403);

        var request = await requestRepository.GetByIdAsync(requestId, tracking: true, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role))
            throw new KeyNotFoundException("Request was not found.");
        if (role == "coordinator" && !RequestAccess.CanManageQuotes(request, actorId, role))
            throw new AuthException("forbidden", "Only the coordinator that owns the request can assign it.", 403);
        var inspector = await userRepository.GetByIdAsync(inspectorId, cancellationToken);
        if (inspector is null || !inspector.IsActive || inspector.Role != UserRole.Inspector)
            throw new InvalidOperationException("The selected inspector is unavailable.");
        if (inspector.ManagerId != request.ManagerId)
            throw new AuthException(
                "forbidden",
                "The selected inspector does not belong to the request team.",
                403);
        if (inspector.IsOnLeave && (inspector.LeaveUntil is null || inspector.LeaveUntil >= scheduledAt))
            throw new InvalidOperationException("The selected inspector is on leave at the scheduled time.");

        await ApplyReviewerAsync(request, reviewerId, actorId, role, cancellationToken);
        request.AssignInspector(inspectorId, scheduledAt, actorId, note);
        await requestRepository.SaveAsync(request, cancellationToken);
        return RequestResponseMapper.Map(request, await ReviewerNameAsync(request, cancellationToken));
    }

    /// <summary>
    /// Moves the inspection to another date. The inspector of the job may do it (and nothing else on it), and so may the
    /// coordinator that owns the request, a manager or Admin, until the inspector starts the work.
    /// </summary>
    public async Task<RequestResponse> ChangeInspectionDateAsync(Guid requestId, Guid actorId, string role, DateTimeOffset scheduledAt, string? note, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: true, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role))
            throw new KeyNotFoundException("Request was not found.");
        if (role == "coordinator" && !RequestAccess.CanManageQuotes(request, actorId, role))
            throw new AuthException("forbidden", "Only the coordinator that owns the request can change its date.", 403);
        if (request.InspectorId is Guid inspectorId && role != "inspector")
        {
            var inspector = await userRepository.GetByIdAsync(inspectorId, cancellationToken);
            if (inspector is { IsOnLeave: true } && (inspector.LeaveUntil is null || inspector.LeaveUntil >= scheduledAt))
                throw new InvalidOperationException("The inspector is on leave at the new date.");
        }

        request.ChangeInspectionDate(scheduledAt, actorId, note);
        await requestRepository.SaveAsync(request, cancellationToken);
        return RequestResponseMapper.Map(request);
    }

    /// <summary>Loads a request for editing: it must be visible to the actor and owned by them.</summary>
    private async Task<InspectionRequest> LoadForEditAsync(
        Guid requestId,
        Guid actorId,
        string role,
        CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: true, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role))
            throw new KeyNotFoundException("Request was not found.");
        if (request.CoordinatorId != actorId)
            throw new AuthException("forbidden", "Only the coordinator that owns the request can edit it.", 403);
        return request;
    }

    // Machines that are not in our records yet are registered with the request: same save, same transaction.
    private Task RegisterEquipmentAsync(InspectionRequest request, CancellationToken cancellationToken) =>
        equipmentRegistrar is null ? Task.CompletedTask : equipmentRegistrar.RegisterAsync(request, cancellationToken);

    private static RequestItem CreateItem(CreateRequestItem item) => RequestItem.Create(
        item.Name,
        Certificates.GearRules.QuantityFor(item.Name, item.Quantity),
        item.EquipmentId,
        item.Type,
        item.Capacity,
        item.SerialNumber,
        location: item.Location,
        issueDescription: item.IssueDescription);

    private static void AuthorizeTransition(InspectionRequest request, Guid actorId, string role, JobStatus target)
    {
        // Quotation steps and the inspector assignment have their own endpoints that keep the quotation, the email and
        // the inspector in step with the request. Moving the status directly would leave them out of sync.
        if (target is JobStatus.QuoteDraft or JobStatus.QuoteSent or JobStatus.CounterOfferSent or JobStatus.ClientCountered or JobStatus.QuoteRejected
            or JobStatus.ClientDeclined or JobStatus.Unassigned)
            throw new AuthException(
                "quote_flow",
                "Quotation steps change through the quotation endpoints (quotes, send, accept, reject), not through transitions.",
                409);
        if (target == JobStatus.Assigned)
            throw new AuthException("assignment_flow", "Assign an inspector through the assignment endpoint.", 409);
        // The same for the certificates: moving the job to a review state by hand leaves it without certificates, so the
        // technical manager would see a job with nothing to review. These states follow from the Quotation queue steps.
        if (target is JobStatus.CertificateSubmitted or JobStatus.CertificateReturned or JobStatus.CertificateApproved or JobStatus.SentToClient)
            throw new AuthException(
                "certificate_flow",
                "Certificate steps change through the Quotation queue: the inspector submits each machine, the technical manager uses approve-all or return-to-inspector, the coordinator uses deliver-to-client.",
                409);

        // What is left is the work after acceptance: inspectors move their own jobs, managers and admins the rest.
        var allowed = role is "admin" or "technical_manager"
            || (role == "inspector" && request.InspectorId == actorId &&
                target is JobStatus.TaskAccepted or JobStatus.InProgress);
        if (!allowed) throw new AuthException("forbidden", "Role or ownership does not permit this transition.", 403);
    }


    private static bool IsInspector(string role) => role == "inspector";
    // A new request starts with the reviewer the creator picked, or with the company's default.
    private async Task UseReviewerAsync(InspectionRequest job, Guid companyId, Guid? chosen, CancellationToken cancellationToken)
    {
        if (reviewers is null) return;
        job.UseReviewer(chosen is { } picked
            ? (await reviewers.RequireReviewerAsync(companyId, picked, cancellationToken)).Id
            : await reviewers.DefaultReviewerIdAsync(companyId, cancellationToken));
    }

    // When a job is given to an inspector its reviewer is settled: the one named, else the one it has if still usable, else the default.
    // Only the coordinator that owns the request (or the platform owner) picks a reviewer: a technical manager must not make themselves one.
    private async Task ApplyReviewerAsync(InspectionRequest job, Guid? requested, Guid actorId, string role, CancellationToken cancellationToken)
    {
        if (reviewers is null || job.ManagerId is not Guid companyId) return;

        if (requested is { } wanted && wanted != job.ReviewerId)
        {
            if (role is not ("admin" or "coordinator"))
                throw new AuthException("forbidden", "Only the coordinator chooses the reviewer of a job.", 403);
            var next = await reviewers.RequireReviewerAsync(companyId, wanted, cancellationToken);
            var previous = job.ReviewerId is { } before ? await userRepository.GetByIdAsync(before, cancellationToken) : null;
            job.ChangeReviewer(next.Id, actorId, previous?.FullName ?? "no reviewer", next.FullName);
            return;
        }

        var stillUsable = job.ReviewerId is { } current &&
            await userRepository.GetByIdAsync(current, cancellationToken) is { Role: UserRole.TechnicalManager, IsActive: true };
        if (!stillUsable) job.UseReviewer(await reviewers.DefaultReviewerIdAsync(companyId, cancellationToken));
        if (job.ReviewerId is null)
            throw new AuthException("no_reviewer", "The company has no active technical manager to review the certificates. Create one first.", 409);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ReviewerNamesAsync(IReadOnlyCollection<InspectionRequest> found, CancellationToken cancellationToken)
    {
        var ids = found.Select(item => item.ReviewerId).OfType<Guid>().Distinct().ToArray();
        return ids.Length == 0 ? new Dictionary<Guid, string>() : await userRepository.GetFullNamesAsync(ids, cancellationToken);
    }

    private async Task<string?> ReviewerNameAsync(InspectionRequest job, CancellationToken cancellationToken) =>
        job.ReviewerId is { } id ? (await userRepository.GetFullNamesAsync([id], cancellationToken)).GetValueOrDefault(id) : null;

    private static string? ReviewerNameOf(InspectionRequest item, IReadOnlyDictionary<Guid, string> names) =>
        item.ReviewerId is { } id && names.TryGetValue(id, out var name) ? name : null;

}
