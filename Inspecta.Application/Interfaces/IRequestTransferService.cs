using Inspecta.Application.Contracts.Requests;

namespace Inspecta.Application.Interfaces;

public interface IRequestTransferService
{
    /// <summary>
    /// Hands the request to another coordinator of the same team. The coordinator that owns it can do it until a quotation was sent; the
    /// technical manager and the admin can do it at any time before the request is finished.
    /// </summary>
    Task<RequestResponse> TransferAsync(Guid requestId, Guid actorId, string role, TransferRequest transfer, CancellationToken cancellationToken = default);

    /// <summary>The active coordinators of the caller's team, the caller left out. The admin names the team with <paramref name="managerId"/>.</summary>
    Task<IReadOnlyList<TeamCoordinatorResponse>> ListCoordinatorsAsync(Guid actorId, string role, Guid? managerId, CancellationToken cancellationToken = default);
}
