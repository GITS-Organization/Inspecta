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
/// <param name="ExcludeStatuses">Leave out requests in these workflow statuses.</param>
/// <param name="Search">Case-insensitive text matched against client, subject, sender and location.</param>
/// <param name="Skip">Rows to skip, for paging.</param>
/// <param name="Take">Maximum rows to return.</param>
public sealed record RequestListFilter(
    Guid? InspectorId = null,
    Guid? CoordinatorId = null,
    Guid? ManagerId = null,
    RequestTriage? Triage = null,
    JobStatus? Status = null,
    IReadOnlyCollection<JobStatus>? ExcludeStatuses = null,
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

    /// <summary>Adds and removes the items of a revision in the change tracker without saving; the caller saves.</summary>
    void StageRevision(RequestItemChanges changes) { }

    /// <summary>Saves a revised request together with the items it added and removed, in one save.</summary>
    Task SaveRevisionAsync(
        InspectionRequest request,
        RequestItemChanges changes,
        CancellationToken cancellationToken = default) => SaveAsync(request, cancellationToken);

    Task RemoveItemAsync(InspectionRequest request, RequestItem item, CancellationToken cancellationToken = default);
    Task<InspectionRequest?> GetByIdAsync(Guid requestId, bool tracking, CancellationToken cancellationToken = default);
    Task<RequestEmail?> GetEmailAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InspectionRequest>> ListAsync(RequestListFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequestCount>> CountAsync(RequestListFilter scope, CancellationToken cancellationToken = default);
    /// <summary>Number of unfinished jobs (assigned up to certificate returned) per inspector.</summary>
    Task<IReadOnlyDictionary<Guid, int>> CountActiveByInspectorAsync(
        IReadOnlyCollection<Guid> inspectorIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

    Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<Guid?> GetClientManagerIdAsync(Guid clientId, CancellationToken cancellationToken = default);
}
