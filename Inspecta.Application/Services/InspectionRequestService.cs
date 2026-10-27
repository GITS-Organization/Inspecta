using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class InspectionRequestService(IRequestRepository requestRepository, IUserRepository userRepository) : IInspectionRequestService
{
    public async Task<RequestResponse> CreateAsync(CreateInspectionRequest request, Guid coordinatorId, CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0) throw new ArgumentException("At least one request item is required.");
        if (request.ClientId is Guid clientId && !await requestRepository.ClientExistsAsync(clientId, cancellationToken))
            throw new KeyNotFoundException("Client was not found.");

        var aggregate = InspectionRequest.Create(
            request.ClientName,
            request.Location,
            request.ClientId,
            request.Items.Select(item => RequestItem.Create(item.Name, item.Quantity, item.EquipmentId, item.Type, item.Capacity, item.SerialNumber)),
            coordinatorId,
            subject: request.Subject,
            emailThreadId: request.EmailThreadId,
            preferredDate: request.PreferredDate,
            accessNotes: request.AccessNotes);
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
        if (inspector.IsOnLeave && (inspector.LeaveUntil is null || inspector.LeaveUntil >= scheduledAt))
            throw new InvalidOperationException("The selected inspector is on leave at the scheduled time.");

        request.AssignInspector(inspectorId, scheduledAt, actorId, note);
        await requestRepository.SaveAsync(request, cancellationToken);
        return Map(request);
    }

    private static void AuthorizeTransition(InspectionRequest request, Guid actorId, string role, JobStatus target)
    {
        var allowed = role == "inspector" && request.InspectorId == actorId &&
            target is JobStatus.TaskAccepted or JobStatus.InProgress;
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
        request.Items.Select(item => new RequestItemResponse(item.Id, item.EquipmentId, item.Name, item.Type, item.Capacity, item.Quantity, item.SerialNumber)).ToArray(),
        request.History.OrderBy(item => item.OccurredAt)
            .Select(item => new RequestHistoryResponse(item.FromStatus, item.ToStatus, item.ActorId, item.OccurredAt, item.Note)).ToArray());
}