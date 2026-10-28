using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

/// <summary>Which requests a list call may return, beyond the tenant filter applied by the data layer.</summary>
/// <param name="InspectorId">Only requests assigned to this inspector.</param>
/// <param name="CoordinatorId">
/// Coordinator scope: manual requests stay visible to the whole team, email requests only to the coordinator that received them.
/// </param>
/// <param name="ManagerId">Only requests of this team (used by Admin to narrow the global view).</param>
public sealed record RequestListFilter(Guid? InspectorId = null, Guid? CoordinatorId = null, Guid? ManagerId = null);

public interface IRequestRepository
{
    Task AddAsync(InspectionRequest request, CancellationToken cancellationToken = default);
    Task SaveAsync(InspectionRequest request, CancellationToken cancellationToken = default);
    Task AddItemAsync(InspectionRequest request, RequestItem item, CancellationToken cancellationToken = default);
    Task RemoveItemAsync(InspectionRequest request, RequestItem item, CancellationToken cancellationToken = default);
    Task<InspectionRequest?> GetByIdAsync(Guid requestId, bool tracking, CancellationToken cancellationToken = default);
    Task<RequestEmail?> GetEmailAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InspectionRequest>> ListAsync(RequestListFilter filter, CancellationToken cancellationToken = default);
    Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<Guid?> GetClientManagerIdAsync(Guid clientId, CancellationToken cancellationToken = default);
}
