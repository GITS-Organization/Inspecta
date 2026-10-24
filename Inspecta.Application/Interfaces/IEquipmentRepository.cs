using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IEquipmentRepository
{
    Task AddAsync(Equipment equipment, CancellationToken cancellationToken = default);
    Task<Equipment?> GetByIdAsync(Guid equipmentId, bool tracking, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Equipment>> ListAsync(Guid? clientId, bool includeArchived, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default);
}