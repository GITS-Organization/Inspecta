using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class InspectionRequestService(
    IRequestRepository requestRepository,
    IUserRepository userRepository,
    IRequestEquipmentRegistrar? equipmentRegistrar = null) : IInspectionRequestService
{
    public async Task<RequestResponse> CreateAsync(
        CreateInspectionRequest request,
        Guid coordinatorId,
        Guid? managerId,
        CancellationToken cancellationToken = default)
        => await CreateCoreAsync(request, coordinatorId, managerId, RequestSource.Manual, null, null, cancellationToken);

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
            var manager = await userRepository.GetByIdAsync(managerIdValue, cancellationToken);
            if (manager is null || !manager.IsActive || manager.Role != UserRole.TechnicalManager)
                throw new ArgumentException("The selected technical manager is unavailable.", nameof(managerId));
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
        await requestRepository.AddAsync(aggregate, cancellationToken);
        return RequestResponseMapper.Map(aggregate);
    }

    public async Task<IReadOnlyList<RequestResponse>> ListVisibleAsync(
        Guid actorId,
        string role,
        RequestListQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, RequestListQuery.MaxPageSize);
        var page = Math.Max(query.Page, 1);
        var filter = ScopeFor(actorId, role, query.ManagerId) with
        {
            Triage = query.Triage ?? RequestTriage.Request,
            Status = query.Status,
            ExcludeStatuses = query.Status is null ? InQuotation : null,
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Skip = (page - 1) * pageSize,
            Take = pageSize
        };
        return (await requestRepository.ListAsync(filter, cancellationToken)).Select(RequestResponseMapper.Map).ToArray();
    }

    public async Task<RequestSummaryResponse> GetSummaryAsync(
        Guid actorId,
        string role,
        Guid? managerId = null,
        CancellationToken cancellationToken = default)
    {
        var counts = await requestRepository.CountAsync(ScopeFor(actorId, role, managerId), cancellationToken);
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
        request.Confirm();
        await requestRepository.SaveAsync(request, cancellationToken);
        return RequestResponseMapper.Map(request);
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
    private static readonly JobStatus[] InQuotation = [JobStatus.QuoteDraft, JobStatus.QuoteSent, JobStatus.ClientCountered];

    private static RequestListFilter ScopeFor(Guid actorId, string role, Guid? managerId) => new(
        InspectorId: IsInspector(role) ? actorId : null,
        CoordinatorId: role == "coordinator" ? actorId : null,
        ManagerId: role == "admin" ? managerId : null);

    public async Task<RequestResponse?> GetVisibleAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role)) return null;
        return RequestResponseMapper.Map(request);
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
        return RequestResponseMapper.Map(request);
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
            itemId, item.Name, item.Quantity, item.EquipmentId, item.Type, item.Capacity,
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
            "technical_manager" => actorId,
            "coordinator" => (await userRepository.GetByIdAsync(actorId, cancellationToken))?.ManagerId,
            _ => managerId
        };
        if (role == "coordinator" && team is null) return [];

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
    public async Task<RequestResponse> AssignAsync(Guid requestId, Guid actorId, string role, Guid inspectorId, DateTimeOffset scheduledAt, string? note, CancellationToken cancellationToken = default)
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

        request.AssignInspector(inspectorId, scheduledAt, actorId, note);
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
        item.Quantity,
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
        if (target is JobStatus.QuoteDraft or JobStatus.QuoteSent or JobStatus.ClientCountered or JobStatus.QuoteRejected
            or JobStatus.ClientDeclined or JobStatus.QuoteAccepted)
            throw new AuthException(
                "quote_flow",
                "Quotation steps change through the quotation endpoints (quotes, send, accept, reject), not through transitions.",
                409);
        if (target == JobStatus.Assigned)
            throw new AuthException("assignment_flow", "Assign an inspector through the assignment endpoint.", 409);

        // What is left is the work after acceptance: inspectors move their own jobs, managers and admins the rest.
        var allowed = role is "admin" or "technical_manager"
            || (role == "inspector" && request.InspectorId == actorId &&
                target is JobStatus.TaskAccepted or JobStatus.InProgress);
        if (!allowed) throw new AuthException("forbidden", "Role or ownership does not permit this transition.", 403);
    }


    private static bool IsInspector(string role) => role == "inspector";
}
