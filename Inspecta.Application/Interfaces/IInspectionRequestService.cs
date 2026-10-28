using Inspecta.Application.Contracts.Requests;

namespace Inspecta.Application.Interfaces;

public interface IInspectionRequestService
{
    Task<RequestResponse> CreateAsync(CreateInspectionRequest request, Guid coordinatorId, Guid? managerId, CancellationToken cancellationToken = default);
    Task<RequestResponse> CreateFromEmailAsync(CreateInspectionRequest request, string senderEmail, string emailBody, Guid coordinatorId, Guid? managerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequestResponse>> ListVisibleAsync(Guid actorId, string role, Guid? managerId = null, CancellationToken cancellationToken = default);
    Task<RequestResponse?> GetVisibleAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<RequestEmailResponse?> GetEmailAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<RequestResponse> UpdateDetailsAsync(Guid requestId, Guid actorId, string role, UpdateRequestDetailsRequest request, CancellationToken cancellationToken = default);
    Task<RequestResponse> AddItemAsync(Guid requestId, Guid actorId, string role, CreateRequestItem item, CancellationToken cancellationToken = default);
    Task<RequestResponse> UpdateItemAsync(Guid requestId, Guid itemId, Guid actorId, string role, CreateRequestItem item, CancellationToken cancellationToken = default);
    Task<RequestResponse> RemoveItemAsync(Guid requestId, Guid itemId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<RequestResponse> TransitionAsync(Guid requestId, Guid actorId, string role, TransitionRequest request, CancellationToken cancellationToken = default);
    Task<RequestResponse> AssignAsync(Guid requestId, Guid actorId, Guid inspectorId, DateTimeOffset scheduledAt, string? note, CancellationToken cancellationToken = default);
}
