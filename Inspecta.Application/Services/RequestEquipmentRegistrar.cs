using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

/// <summary>
/// Keeps the machines of a request tied to its client. A machine picked by id must be one of the request client's.
/// With <paramref name="createMissing"/> (the original behaviour) machines and clients that are not in the records are
/// created from the typed text; without it they are only linked when they already exist, so the records stay the ones
/// the team registered.
/// </summary>
public sealed class RequestEquipmentRegistrar(IEquipmentCatalog catalog, bool createMissing = true) : IRequestEquipmentRegistrar
{
    public async Task RegisterAsync(InspectionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ManagerId is not Guid managerId || !request.CanEditDetails) return;
        await EnsureMachinesBelongToTheClientAsync(request, cancellationToken);

        var unmatched = request.Items.Where(item => item.EquipmentId is null).ToList();
        if (unmatched.Count == 0) return;

        var clientId = request.ClientId ?? await ResolveClientAsync(request, managerId, cancellationToken);
        if (clientId is null) return;

        // Items that are the same machine inside one save share a single record.
        var registered = new Dictionary<string, Item>(StringComparer.Ordinal);
        foreach (var item in unmatched)
        {
            var key = $"{item.Name.Trim().ToLowerInvariant()}|{Normalize(item.SerialNumber)?.ToLowerInvariant()}|{Normalize(item.Capacity)?.ToLowerInvariant()}";
            if (!registered.TryGetValue(key, out var equipment))
            {
                equipment = await catalog.FindEquipmentAsync(clientId.Value, item.Name, item.SerialNumber, item.Capacity, cancellationToken)
                    ?? (createMissing ? CreateEquipment(clientId.Value, item) : null);
                if (equipment is null) continue;
                registered[key] = equipment;
            }

            item.MatchEquipment(equipment.Id);
        }
    }

    private async Task EnsureMachinesBelongToTheClientAsync(InspectionRequest request, CancellationToken cancellationToken)
    {
        var picked = request.Items.Where(item => item.EquipmentId is not null).Select(item => item.EquipmentId!.Value).Distinct().ToList();
        if (picked.Count == 0) return;

        var owners = await catalog.GetEquipmentOwnersAsync(picked, cancellationToken);
        foreach (var equipmentId in picked)
        {
            if (!owners.TryGetValue(equipmentId, out var owner))
                throw new ArgumentException("The selected machine was not found.");
            if (request.ClientId is Guid clientId && owner != clientId)
                throw new ArgumentException("The selected machine does not belong to the client of this request. Choose one of the client's machines.");
        }
    }

    private async Task<Guid?> ResolveClientAsync(InspectionRequest request, Guid managerId, CancellationToken cancellationToken)
    {
        var name = request.ClientName?.Trim();
        if (string.IsNullOrEmpty(name)) return null;

        var client = await catalog.FindClientByNameAsync(managerId, name, cancellationToken);
        if (client is null)
        {
            if (!createMissing) return null;
            client = Client.Create(
                name,
                location: string.IsNullOrWhiteSpace(request.Location) ? null : request.Location,
                notes: "Created automatically from a request.",
                managerId: managerId);
            catalog.Stage(client);
        }

        request.SetClient(client.Id);
        return client.Id;
    }

    private Item CreateEquipment(Guid clientId, RequestItem item)
    {
        var equipment = Item.Create(clientId, item.Name);
        equipment.UpdateDetails(
            item.Name,
            item.Category,
            item.Manufacturer,
            item.Model,
            item.SerialNumber,
            item.Capacity,
            ItemType.Equipment,
            item.Location);
        catalog.Stage(equipment);
        return equipment;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
