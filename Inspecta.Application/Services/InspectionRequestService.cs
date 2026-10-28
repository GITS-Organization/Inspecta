using Inspecta.Application.Contracts.Requests;
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
                throw new Inspecta.Application.Exceptions.AuthException(
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
            request.Items.Select(item => RequestItem.Create(
                item.Name,
                item.Quantity,
                item.EquipmentId,
                item.Type,
                item.Capacity,
                item.SerialNumber,
                issueDescription: item.IssueDescription)),
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
        return Map(aggregate);
    }

    public async Task<IReadOnlyList<RequestResponse>> ListVisibleAsync(Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        Guid? inspectorId = IsInspector(role) ? actorId : null;
        return (await requestRepository.ListAsync(inspectorId, cancellationToken)).Select(Map).ToArray();
    }

    public async Task<RequestResponse?> GetVisibleAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken);
        if (request is null || (IsInspector(role) && request.InspectorId != actorId)) return null;
        return Map(request);
    }

    public async Task<RequestResponse> TransitionAsync(Guid requestId, Guid actorId, string role, TransitionRequest transition, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Request was not found.");
        AuthorizeTransition(request, actorId, role, transition.Status);
        request.TransitionTo(transition.Status, actorId, transition.Note);
        await requestRepository.SaveAsync(request, cancellationToken);
        return Map(request);
    }

    public async Task<RequestResponse> AssignAsync(Guid requestId, Guid actorId, Guid inspectorId, DateTimeOffset scheduledAt, string? note, CancellationToken cancellationToken = default)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Request was not found.");
        var inspector = await userRepository.GetByIdAsync(inspectorId, cancellationToken);
        if (inspector is null || !inspector.IsActive || inspector.Role != UserRole.Inspector)
            throw new InvalidOperationException("The selected inspector is unavailable.");
        if (inspector.ManagerId != request.ManagerId)
            throw new Inspecta.Application.Exceptions.AuthException(
                "forbidden",
                "The selected inspector does not belong to the request team.",
                403);
        if (inspector.IsOnLeave && (inspector.LeaveUntil is null || inspector.LeaveUntil >= scheduledAt))
            throw new InvalidOperationException("The selected inspector is on leave at the scheduled time.");

        request.AssignInspector(inspectorId, scheduledAt, actorId, note);
        await requestRepository.SaveAsync(request, cancellationToken);
        return Map(request);
    }

    private static void AuthorizeTransition(InspectionRequest request, Guid actorId, string role, JobStatus target)
    {
        var allowed = role is "admin" or "technical_manager"
            || (role == "inspector" && request.InspectorId == actorId &&
                target is JobStatus.TaskAccepted or JobStatus.InProgress)
            || (role == "coordinator" &&
                target is JobStatus.QuoteDraft or JobStatus.QuoteSent or JobStatus.ClientCountered
                    or JobStatus.QuoteRejected or JobStatus.ClientDeclined or JobStatus.QuoteAccepted);
        if (!allowed) throw new Inspecta.Application.Exceptions.AuthException("forbidden", "Role or ownership does not permit this transition.", 403);
    }

    private static bool IsInspector(string role) => role == "inspector";

    private static RequestResponse Map(InspectionRequest request) => new(
        request.Id,
        request.ClientId,
        request.ClientName,
        request.Location,
        request.Subject,
        request.ReceivedAt,
        request.PreferredDate,
        request.Status,
        request.CoordinatorId,
        request.InspectorId,
        request.ScheduledAt,
        request.Source,
        request.EmailFrom,
        request.EmailPreview,
        request.Items.Select(item => new RequestItemResponse(
            item.Id, item.EquipmentId, item.Name, item.Type, item.Capacity, item.Quantity, item.SerialNumber, item.IssueDescription)).ToArray(),
        request.History.OrderBy(item => item.OccurredAt)
            .Select(item => new RequestHistoryResponse(item.FromStatus, item.ToStatus, item.ActorId, item.OccurredAt, item.Note)).ToArray());
}