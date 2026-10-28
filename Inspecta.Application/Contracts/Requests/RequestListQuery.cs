using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Requests;

/// <summary>Filters and paging for the request list.</summary>
/// <param name="ManagerId">Admin only: narrow the global view to one team.</param>
/// <param name="Triage">Defaults to real requests; use <c>Maybe</c> for the unconfirmed emails.</param>
public sealed record RequestListQuery(
    Guid? ManagerId = null,
    RequestTriage? Triage = null,
    JobStatus? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = RequestListQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;
}

/// <summary>Counts for the request tabs, within what the current user can see.</summary>
public sealed record RequestSummaryResponse(
    int Requests,
    int Maybe,
    IReadOnlyList<RequestStatusCount> ByStatus);

/// <summary>Number of real requests in one workflow status.</summary>
public sealed record RequestStatusCount(JobStatus Status, int Count);
