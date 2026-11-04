using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IEquipmentRepository
{
    Task AddAsync(Item equipment, CancellationToken cancellationToken = default);
    Task<Item?> GetByIdAsync(Guid equipmentId, bool tracking, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Item>> ListAsync(Guid? clientId, bool includeArchived, CancellationToken cancellationToken = default);
    /// <summary>How many requests and issued certificates still point at the item (they block a permanent delete).</summary>
    Task<int> CountUsagesAsync(Guid equipmentId, CancellationToken cancellationToken = default);
    Task RemoveAsync(Item equipment, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default);
}