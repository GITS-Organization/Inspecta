using Inspecta.Application.Contracts.Executive;

namespace Inspecta.Application.Interfaces;

public interface IExecutiveService
{
    /// <summary>The company's targets, or the defaults when it never set them (and for the platform owner without a company).</summary>
    Task<ExecutiveTargetsResponse> GetTargetsAsync(Guid? managerId, CancellationToken cancellationToken = default);

    Task<ExecutiveTargetsResponse> SaveTargetsAsync(Guid managerId, SaveExecutiveTargetsRequest request, Guid actorId, CancellationToken cancellationToken = default);
}
