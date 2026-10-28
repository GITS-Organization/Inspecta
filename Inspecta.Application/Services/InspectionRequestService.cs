using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class InspectionRequestService(IRequestRepository requestRepository, IUserRepository userRepository) : IInspectionRequestService
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
        Guid? managerId = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new RequestListFilter(
            InspectorId: IsInspector(role) ? actorId : null,
            CoordinatorId: role == "coordinator" ? actorId : null,
            ManagerId: role == "admin" ? managerId : null);
        return (await requestRepository.ListAsync(filter, cancellationToken)).Select(RequestResponseMapper.Map).ToArray();
    }

    public async Task<RequestResponse?> GetVisibleAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken);
        if (request is null || !IsVisible(request, actorId, role)) return null;
        return RequestResponseMapper.Map(request);
    }

    public async Task<RequestEmailResponse?> GetEmailAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken);
        if (request is null || !IsVisible(request, actorId, role)) return null;

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
        if (details.ClientId is Guid clientId)
        {
            if (!await requestRepository.ClientExistsAsync(clientId, cancellationToken))
                throw new KeyNotFoundException("Client was not found.");
            if (await requestRepository.GetClientManagerIdAsync(clientId, cancellationToken) != request.ManagerId)
                throw new AuthException("forbidden", "The client does not belong to the request team.", 403);
        }

        request.UpdateDetails(details.ClientName, details.Location, details.ClientId, details.PreferredDate, details.AccessNotes);
        await requestRepository.SaveAsync(request, cancellationToken);
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
        if (request is null || !IsVisible(request, actorId, role))
            throw new KeyNotFoundException("Request was not found.");
        AuthorizeTransition(request, actorId, role, transition.Status);
        request.TransitionTo(transition.Status, actorId, transition.Note);
        await requestRepository.SaveAsync(request, cancellationToken);
        return RequestResponseMapper.Map(request);
    }

    public async Task<RequestResponse> AssignAsync(Guid requestId, Guid actorId, Guid inspectorId, DateTimeOffset scheduledAt, string? note, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Request was not found.");
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
        if (request is null || !IsVisible(request, actorId, role))
            throw new KeyNotFoundException("Request was not found.");
        if (request.CoordinatorId != actorId)
            throw new AuthException("forbidden", "Only the coordinator that owns the request can edit it.", 403);
        return request;
    }

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
        var allowed = role is "admin" or "technical_manager"
            || (role == "inspector" && request.InspectorId == actorId &&
                target is JobStatus.TaskAccepted or JobStatus.InProgress)
            || (role == "coordinator" &&
                target is JobStatus.QuoteDraft or JobStatus.QuoteSent or JobStatus.ClientCountered
                    or JobStatus.QuoteRejected or JobStatus.ClientDeclined or JobStatus.QuoteAccepted);
        if (!allowed) throw new AuthException("forbidden", "Role or ownership does not permit this transition.", 403);
    }

    // Inspectors only see their assigned jobs. Coordinators see manual requests of their team but only the
    // email requests that arrived in their own mailbox. Team scope for managers and the global Admin view
    // come from the data layer tenant filter.
    private static bool IsVisible(InspectionRequest request, Guid actorId, string role) => role switch
    {
        "inspector" => request.InspectorId == actorId,
        "coordinator" => request.Source != RequestSource.Email || request.CoordinatorId == actorId,
        _ => true
    };

    private static bool IsInspector(string role) => role == "inspector";
}
