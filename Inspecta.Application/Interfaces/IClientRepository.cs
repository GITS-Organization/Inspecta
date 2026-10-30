using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IClientRepository
{
    Task AddAsync(Client client, CancellationToken cancellationToken = default);
    Task<Client?> GetByIdAsync(Guid clientId, bool tracking, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Client>> ListAsync(bool includeArchived, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InspectionRequest>> GetRequestHistoryAsync(Guid clientId, CancellationToken cancellationToken = default);
    /// <summary>How many requests and registered items still point at the client (they block a permanent delete).</summary>
    Task<(int Requests, int Items)> CountDependenciesAsync(Guid clientId, CancellationToken cancellationToken = default);

    Task RemoveAsync(Client client, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}