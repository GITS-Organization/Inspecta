using Inspecta.Application.Contracts.Executive;

namespace Inspecta.Application.Interfaces;

public interface IExecutiveService
{
    /// <summary>The company's targets, or the defaults when it never set them (and for the platform owner without a company).</summary>
    Task<ExecutiveTargetsResponse> GetTargetsAsync(Guid? managerId, CancellationToken cancellationToken = default);

    Task<ExecutiveTargetsResponse> SaveTargetsAsync(Guid managerId, SaveExecutiveTargetsRequest request, Guid actorId, CancellationToken cancellationToken = default);

    /// <summary>The Money screen: collected against invoiced, what is still owed, its age, who owes the most and the last twelve months.</summary>
    Task<MoneyResponse> MoneyAsync(ExecutiveQuery query, CancellationToken cancellationToken = default);

    /// <summary>The Renewals screen: machines due for inspection by bucket, week and client, and the machines themselves (a filter, paged).</summary>
    Task<RenewalsResponse> RenewalsAsync(ExecutiveQuery query, string? filter, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The Clients screen: counters and the clients with their health, filtered and paged.</summary>
    Task<ClientsResponse> ClientsAsync(ExecutiveQuery query, string? health, string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>One client, or <c>null</c> when it does not exist, is archived or is not the company's.</summary>
    Task<ClientDetailResponse?> ClientAsync(ExecutiveQuery query, Guid clientId, CancellationToken cancellationToken = default);
}
