using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Interfaces;

/// <summary>Which requests a list call may return, beyond the tenant filter applied by the data layer.</summary>
/// <param name="InspectorId">Only requests assigned to this inspector.</param>
/// <param name="CoordinatorId">
/// Coordinator scope: manual requests stay visible to the whole team, email requests only to the coordinator that received them.
/// </param>
/// <param name="ManagerId">Only requests of this team (used by Admin to narrow the global view).</param>
/// <param name="Triage">Real requests or "maybe" emails.</param>
/// <param name="Status">Only requests in this workflow status.</param>
/// <param name="Search">Case-insensitive text matched against client, subject, sender and location.</param>
/// <param name="Skip">Rows to skip, for paging.</param>
/// <param name="Take">Maximum rows to return.</param>
public sealed record RequestListFilter(
    Guid? InspectorId = null,
    Guid? CoordinatorId = null,
    Guid? ManagerId = null,
    RequestTriage? Triage = null,
    JobStatus? Status = null,
    string? Search = null,
    int Skip = 0,
    int Take = int.MaxValue);

/// <summary>Number of visible requests in one triage and status bucket.</summary>
public sealed record RequestCount(RequestTriage Triage, JobStatus Status, int Count);

public interface IRequestRepository
{
    Task AddAsync(InspectionRequest request, CancellationToken cancellationToken = default);
    Task SaveAsync(InspectionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(InspectionRequest request, CancellationToken cancellationToken = default);
    Task AddItemAsync(InspectionRequest request, RequestItem item, CancellationToken cancellationToken = default);
    Task RemoveItemAsync(InspectionRequest request, RequestItem item, CancellationToken cancellationToken = default);
    Task<InspectionRequest?> GetByIdAsync(Guid requestId, bool tracking, CancellationToken cancellationToken = default);
    Task<RequestEmail?> GetEmailAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InspectionRequest>> ListAsync(RequestListFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequestCount>> CountAsync(RequestListFilter scope, CancellationToken cancellationToken = default);
    Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<Guid?> GetClientManagerIdAsync(Guid clientId, CancellationToken cancellationToken = default);
}
