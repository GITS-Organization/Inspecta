using Inspecta.Application.Contracts.Clients;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

public sealed class ClientService(IClientRepository clientRepository) : IClientService
{
    public async Task<ClientResponse> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Contacts.Count(contact => contact.IsPrimary) > 1)
            throw new ArgumentException("A client can have only one primary contact.");
        var client = Client.Create(
            request.Name,
            isCompany: request.IsCompany,
            hasContract: request.HasContract,
            notes: request.Notes);
        foreach (var contact in request.Contacts)
        {
            client.Contacts.Add(ClientContact.Create(client.Id, contact.Name, contact.Email, contact.Phone, contact.IsPrimary));
        }
        await clientRepository.AddAsync(client, cancellationToken);
        return Map(client);
    }

    public async Task<IReadOnlyList<ClientResponse>> ListAsync(bool includeArchived, CancellationToken cancellationToken = default) =>
        (await clientRepository.ListAsync(includeArchived, cancellationToken)).Select(Map).ToArray();

    public async Task<ClientResponse?> GetByIdAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetByIdAsync(clientId, tracking: false, cancellationToken);
        return client is null ? null : Map(client);
    }

    public async Task ArchiveAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetByIdAsync(clientId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Client was not found.");
        client.Archive();
        await clientRepository.SaveAsync(cancellationToken);
    }

    private static ClientResponse Map(Client client) => new(
        client.Id,
        client.Name,
        client.IsCompany,
        client.HasContract,
        client.Notes,
        client.CreatedAt,
        client.ArchivedAt,
        client.Contacts.Select(contact => new ClientContactResponse(contact.Id, contact.Name, contact.Email, contact.Phone, contact.IsPrimary)).ToArray());
}