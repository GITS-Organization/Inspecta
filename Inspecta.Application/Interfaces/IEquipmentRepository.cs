using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IEquipmentRepository
{
    Task AddAsync(Item equipment, CancellationToken cancellationToken = default);
    Task<Item?> GetByIdAsync(Guid equipmentId, bool tracking, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Item>> ListAsync(Guid? clientId, bool includeArchived, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default);
}