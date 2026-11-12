using Inspecta.Application.Contracts.Requests;

namespace Inspecta.Application.Interfaces;

public interface IInspectionRequestService
{
    Task<RequestResponse> CreateAsync(CreateInspectionRequest request, Guid coordinatorId, Guid? managerId, CancellationToken cancellationToken = default);
    /// <summary>Creates a man day request and gives it to an inspector straight away (no machines, no quotation).</summary>
    Task<RequestResponse> CreateManDayAsync(CreateManDayRequest request, Guid actorId, string role, Guid? managerId, CancellationToken cancellationToken = default);
    /// <summary>Gives a request of a client with a contract to an inspector straight away (no quotation).</summary>
    Task<RequestResponse> AssignWithoutQuotationAsync(Guid requestId, Guid actorId, string role, AssignWithoutQuotationRequest request, CancellationToken cancellationToken = default);
    Task<RequestResponse> CreateFromEmailAsync(CreateInspectionRequest request, string senderEmail, string emailBody, Guid coordinatorId, Guid? managerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequestResponse>> ListVisibleAsync(Guid actorId, string role, RequestListQuery query, CancellationToken cancellationToken = default);
    Task<RequestSummaryResponse> GetSummaryAsync(Guid actorId, string role, Guid? managerId = null, DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);
    Task<RequestResponse> ConfirmAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MachineSuggestionResponse>> SuggestMachinesAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<RequestResponse> AutofillAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task DeleteMaybeAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<RequestResponse?> GetVisibleAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<RequestEmailResponse?> GetEmailAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<RequestResponse> UpdateDetailsAsync(Guid requestId, Guid actorId, string role, UpdateRequestDetailsRequest request, CancellationToken cancellationToken = default);
    Task<RequestResponse> AddItemAsync(Guid requestId, Guid actorId, string role, CreateRequestItem item, CancellationToken cancellationToken = default);
    Task<RequestResponse> UpdateItemAsync(Guid requestId, Guid itemId, Guid actorId, string role, CreateRequestItem item, CancellationToken cancellationToken = default);
    Task<RequestResponse> RemoveItemAsync(Guid requestId, Guid itemId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<RequestResponse> TransitionAsync(Guid requestId, Guid actorId, string role, TransitionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InspectorResponse>> ListInspectorsAsync(Guid actorId, string role, Guid? managerId = null, CancellationToken cancellationToken = default);
    Task<RequestResponse> AssignAsync(Guid requestId, Guid actorId, string role, Guid inspectorId, DateTimeOffset scheduledAt, string? note, CancellationToken cancellationToken = default, Guid? reviewerId = null);
    Task<RequestResponse> ChangeInspectionDateAsync(Guid requestId, Guid actorId, string role, DateTimeOffset scheduledAt, string? note, CancellationToken cancellationToken = default);
}
