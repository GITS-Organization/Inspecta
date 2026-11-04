using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

public sealed class MachineSuggestionService(IEquipmentCatalog catalog) : IMachineSuggestionService
{
    public async Task<IReadOnlyList<MachineMatch>> FindAsync(
        InspectionRequest request,
        string? textBody,
        string? htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (request.ClientId is not Guid clientId) return [];

        var text = EmailTextCleaner.NewText(request.Subject, textBody, htmlBody);
        if (text.Length == 0) return [];

        var onRequest = request.Items.Where(item => item.EquipmentId is not null).Select(item => item.EquipmentId!.Value).ToHashSet();
        var machines = (await catalog.ListClientEquipmentAsync(clientId, cancellationToken))
            .Where(item => !onRequest.Contains(item.Id))
            .Select(item => new MachineCandidate(
                item.Id, item.Name, item.Category, item.Manufacturer, item.Model, item.SerialNumber, item.AssetTag, item.Capacity, item.Location))
            .ToList();
        return MachineMatcher.Match(text, machines);
    }

    public async Task<IReadOnlyList<RequestItem>> AutofillAsync(
        InspectionRequest request,
        string? textBody,
        string? htmlBody,
        CancellationToken cancellationToken = default)
    {
        if (!request.CanEditDetails || request.Items.Count > 0 || request.ClientId is null) return [];

        var created = new List<RequestItem>();
        foreach (var match in await FindAsync(request, textBody, htmlBody, cancellationToken))
        {
            if (!match.IsConfident) continue;

            var machine = match.Machine;
            created.Add(request.AddItem(RequestItem.Create(
                machine.Name,
                Certificates.GearRules.QuantityFor(machine.Name, match.Quantity),
                machine.EquipmentId,
                type: machine.Name,
                capacity: machine.Capacity,
                serialNumber: machine.SerialNumber,
                category: machine.Category,
                manufacturer: machine.Manufacturer,
                model: machine.Model,
                location: machine.Location)));
        }

        return created;
    }
}
