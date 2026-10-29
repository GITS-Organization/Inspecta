using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class RequestEquipmentRegistrar(IEquipmentCatalog catalog) : IRequestEquipmentRegistrar
{
    public async Task RegisterAsync(InspectionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ManagerId is not Guid managerId || !request.CanEditDetails) return;
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
                    ?? CreateEquipment(clientId.Value, item);
                registered[key] = equipment;
            }

            item.MatchEquipment(equipment.Id);
        }
    }

    private async Task<Guid?> ResolveClientAsync(InspectionRequest request, Guid managerId, CancellationToken cancellationToken)
    {
        var name = request.ClientName?.Trim();
        if (string.IsNullOrEmpty(name)) return null;

        var client = await catalog.FindClientByNameAsync(managerId, name, cancellationToken);
        if (client is null)
        {
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
