using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IClientRepository
{
    Task AddAsync(Client client, CancellationToken cancellationToken = default);
    Task<Client?> GetByIdAsync(Guid clientId, bool tracking, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Client>> ListAsync(bool includeArchived, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}