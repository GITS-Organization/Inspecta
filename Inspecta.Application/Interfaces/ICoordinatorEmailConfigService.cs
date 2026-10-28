using Inspecta.Application.Contracts.Email;

namespace Inspecta.Application.Interfaces;

public interface ICoordinatorEmailConfigService
{
    Task UpdateAsync(
        Guid managerId,
        Guid coordinatorId,
        CoordinatorEmailConfigRequest request,
        CancellationToken cancellationToken = default);
}
