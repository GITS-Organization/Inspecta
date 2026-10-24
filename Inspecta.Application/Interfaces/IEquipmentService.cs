using Inspecta.Application.Contracts.Equipment;

namespace Inspecta.Application.Interfaces;

public interface IEquipmentService
{
    Task<EquipmentResponse> CreateAsync(SaveEquipmentRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EquipmentResponse>> ListAsync(Guid? clientId, bool includeArchived, CancellationToken cancellationToken = default);
    Task<EquipmentResponse?> GetByIdAsync(Guid equipmentId, CancellationToken cancellationToken = default);
    Task<EquipmentResponse> UpdateAsync(Guid equipmentId, SaveEquipmentRequest request, CancellationToken cancellationToken = default);
    Task RecordInspectionAsync(Guid equipmentId, RecordEquipmentInspectionRequest request, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid equipmentId, CancellationToken cancellationToken = default);
}