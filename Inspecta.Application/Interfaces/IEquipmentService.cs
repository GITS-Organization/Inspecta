using Inspecta.Application.Contracts.Equipment;

namespace Inspecta.Application.Interfaces;

public interface IEquipmentService
{
    Task<EquipmentResponse> CreateAsync(SaveEquipmentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EquipmentResponse>> ListAsync(Guid? clientId, bool includeArchived, CancellationToken cancellationToken = default);
    Task<Contracts.PagedList<EquipmentResponse>> ListPageAsync(Guid? clientId, bool includeArchived, int offset, int limit, CancellationToken cancellationToken = default);
    Task<EquipmentResponse?> GetByIdAsync(Guid equipmentId, CancellationToken cancellationToken = default);
    Task<EquipmentResponse> UpdateAsync(Guid equipmentId, SaveEquipmentRequest request, CancellationToken cancellationToken = default);
    Task RecordInspectionAsync(Guid equipmentId, RecordEquipmentInspectionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid equipmentId, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid equipmentId, CancellationToken cancellationToken = default);

    /// <summary>Marks the machine out of service (scrapped or sold). Doing it again changes nothing.</summary>
    Task<EquipmentResponse> RetireAsync(Guid equipmentId, CancellationToken cancellationToken = default);

    /// <summary>Puts a machine back in service.</summary>
    Task<EquipmentResponse> ReactivateAsync(Guid equipmentId, CancellationToken cancellationToken = default);
}