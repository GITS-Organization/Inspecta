using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Contracts.Equipment;

namespace Inspecta.Application.Interfaces;

/// <summary>Changing the machines of a job that is under way: the inspector, the coordinator, the technical manager and the admin can.</summary>
public interface IJobMachineService
{
    /// <summary>The client's registered machines that are not in the job yet.</summary>
    Task<IReadOnlyList<EquipmentResponse>> AvailableAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<MachineListResponse> AddAsync(Guid requestId, Guid actorId, string role, AddMachineRequest machine, CancellationToken cancellationToken = default);

    Task<MachineListResponse> EditAsync(Guid requestId, Guid requestItemId, Guid actorId, string role, EditMachineRequest machine, CancellationToken cancellationToken = default);

    Task<MachineListResponse> RemoveAsync(Guid requestId, Guid requestItemId, Guid actorId, string role, CancellationToken cancellationToken = default);
}
