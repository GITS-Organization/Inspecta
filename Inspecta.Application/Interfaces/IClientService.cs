using Inspecta.Application.Contracts.Clients;

namespace Inspecta.Application.Interfaces;

public interface IClientService
{
    Task<ClientResponse> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientResponse>> ListAsync(bool includeArchived, CancellationToken cancellationToken = default);
    Task<ClientResponse?> GetByIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid clientId, CancellationToken cancellationToken = default);
}