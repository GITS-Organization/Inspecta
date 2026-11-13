using Inspecta.Application.Contracts.Executive;

namespace Inspecta.Application.Interfaces;

public interface IExecutiveService
{
    /// <summary>The company's targets, or the defaults when it never set them (and for the platform owner without a company).</summary>
    Task<ExecutiveTargetsResponse> GetTargetsAsync(Guid? managerId, CancellationToken cancellationToken = default);

    Task<ExecutiveTargetsResponse> SaveTargetsAsync(Guid managerId, SaveExecutiveTargetsRequest request, Guid actorId, CancellationToken cancellationToken = default);

    /// <summary>The Money screen: collected against invoiced, what is still owed, its age, who owes the most and the last twelve months.</summary>
    Task<MoneyResponse> MoneyAsync(ExecutiveQuery query, CancellationToken cancellationToken = default);
}
