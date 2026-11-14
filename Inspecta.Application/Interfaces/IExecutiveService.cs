using Inspecta.Application.Contracts.Executive;

namespace Inspecta.Application.Interfaces;

public interface IExecutiveService
{
    /// <summary>The company's targets, or the defaults when it never set them (and for the platform owner without a company).</summary>
    Task<ExecutiveTargetsResponse> GetTargetsAsync(Guid? managerId, CancellationToken cancellationToken = default);

    Task<ExecutiveTargetsResponse> SaveTargetsAsync(Guid managerId, SaveExecutiveTargetsRequest request, Guid actorId, CancellationToken cancellationToken = default);

    /// <summary>The Money screen: collected against invoiced, what is still owed, its age, who owes the most and the last twelve months.</summary>
    Task<MoneyResponse> MoneyAsync(ExecutiveQuery query, CancellationToken cancellationToken = default);

    /// <summary>The financial manager's Overview: the money figures, what is ready to close, what is not handed over yet, the oldest unpaid and the latest payments.</summary>
    Task<FinanceOverviewResponse> FinanceOverviewAsync(ExecutiveQuery query, CancellationToken cancellationToken = default);

    /// <summary>The Today screen: five numbers with their targets, what needs a decision, the last twelve months and the work in flight.</summary>
    Task<TodayResponse> TodayAsync(ExecutiveQuery query, CancellationToken cancellationToken = default);

    /// <summary>The Renewals screen: machines due for inspection by bucket, week and client, and the machines themselves (a filter, paged).</summary>
    Task<RenewalsResponse> RenewalsAsync(ExecutiveQuery query, string? filter, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The Clients screen: counters and the clients with their health, filtered and paged.</summary>
    Task<ClientsResponse> ClientsAsync(ExecutiveQuery query, string? health, string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>One client, or <c>null</c> when it does not exist, is archived or is not the company's.</summary>
    Task<ClientDetailResponse?> ClientAsync(ExecutiveQuery query, Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>The Work screen: jobs in flight, the open jobs (paged), time in each step, quotations, certificates by month, reviewers and inspectors.</summary>
    Task<WorkResponse> WorkAsync(ExecutiveQuery query, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The company feed (a kind, newest first, a cursor) and what is coming in the next seven days.</summary>
    Task<ActivityResponse> ActivityAsync(Guid? managerId, string? kind, DateTimeOffset? before, int limit, CancellationToken cancellationToken = default);
}
