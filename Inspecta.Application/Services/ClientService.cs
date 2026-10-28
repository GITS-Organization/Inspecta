using Inspecta.Application.Contracts.Clients;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

public sealed class ClientService(
    IClientRepository clientRepository,
    IUserRepository userRepository) : IClientService
{
    public async Task<ClientResponse> CreateAsync(CreateClientRequest request, Guid? managerId, CancellationToken cancellationToken = default)
    {
        if (request.Contacts.Count(contact => contact.IsPrimary) > 1)
            throw new ArgumentException("A client can have only one primary contact.");
        if (managerId is Guid managerIdValue)
        {
            var manager = await userRepository.GetByIdAsync(managerIdValue, cancellationToken);
            if (manager is null || !manager.IsActive || manager.Role != Inspecta.Domain.Enums.UserRole.TechnicalManager)
                throw new ArgumentException("The selected technical manager is unavailable.", nameof(managerId));
        }
        var client = Client.Create(
            request.Name,
            isCompany: request.IsCompany,
            hasContract: request.HasContract,
            notes: request.Notes,
            location: request.Location,
            certificatesEmail: request.CertificatesEmail,
            taxNumber: request.TaxNumber,
            latitude: request.Latitude,
            longitude: request.Longitude,
            managerId: managerId);
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

    public async Task<ClientHistoryResponse?> GetHistoryAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetByIdAsync(clientId, tracking: false, cancellationToken);
        if (client is null) return null;

        var requests = await clientRepository.GetRequestHistoryAsync(clientId, cancellationToken);
        return new ClientHistoryResponse(
            clientId,
            requests.Select(request => new ClientRequestHistoryResponse(
                new RequestResponse(
                    request.Id,
                    request.ClientId,
                    request.ClientName,
                    request.Location,
                    request.Subject,
                    request.ReceivedAt,
                    request.PreferredDate,
                    request.Status,
                    request.CoordinatorId,
                    request.InspectorId,
                    request.ScheduledAt,
                    request.Source,
                    request.EmailFrom,
                    request.EmailPreview,
                    request.Items.Select(item => new RequestItemResponse(
                        item.Id, item.EquipmentId, item.Name, item.Type, item.Capacity, item.Quantity, item.SerialNumber, item.IssueDescription)).ToArray(),
                    request.History.OrderBy(entry => entry.OccurredAt)
                        .Select(entry => new RequestHistoryResponse(
                            entry.FromStatus, entry.ToStatus, entry.ActorId, entry.OccurredAt, entry.Note)).ToArray()),
                request.Certificates.Select(CertificateResponseMapper.Map).ToArray()))
                .ToArray());
    }

    public async Task<ClientResponse> UpdateAsync(Guid clientId, UpdateClientRequest request, CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetByIdAsync(clientId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Client was not found.");
        client.UpdateDetails(
            request.Name,
            request.IsCompany,
            request.HasContract,
            request.Location,
            request.CertificatesEmail,
            request.TaxNumber,
            request.Latitude,
            request.Longitude,
            request.Notes);
        await clientRepository.SaveAsync(cancellationToken);
        return Map(client);
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
        client.Location,
        client.Latitude,
        client.Longitude,
        client.CertificatesEmail,
        client.TaxNumber,
        client.Notes,
        client.CreatedAt,
        client.ArchivedAt,
        client.Contacts.Select(contact => new ClientContactResponse(contact.Id, contact.Name, contact.Email, contact.Phone, contact.IsPrimary)).ToArray());
}