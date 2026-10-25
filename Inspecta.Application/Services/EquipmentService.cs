using Inspecta.Application.Contracts.Equipment;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

public sealed class EquipmentService(IEquipmentRepository equipmentRepository) : IEquipmentService
{
    public async Task<EquipmentResponse> CreateAsync(SaveEquipmentRequest request, CancellationToken cancellationToken = default)
    {
        if (!await equipmentRepository.ClientExistsAsync(request.ClientId, cancellationToken))
            throw new KeyNotFoundException("Client was not found or is archived.");
        var equipment = Item.Create(request.ClientId, request.Name);
        equipment.UpdateDetails(
            request.Name, request.Category, request.Manufacturer, request.Model, request.SerialNumber, request.Capacity,
            request.ItemType ?? Inspecta.Domain.Enums.ItemType.Equipment,
            request.LocationInsideClientLocation, request.Notes);
        equipment.UpdateMetadata(request.Labels, request.Dates, request.Checklist, request.Attachments);
        await equipmentRepository.AddAsync(equipment, cancellationToken);
        return Map(equipment);
    }

    public async Task<IReadOnlyList<EquipmentResponse>> ListAsync(Guid? clientId, bool includeArchived, CancellationToken cancellationToken = default) =>
        (await equipmentRepository.ListAsync(clientId, includeArchived, cancellationToken)).Select(Map).ToArray();

    public async Task<EquipmentResponse?> GetByIdAsync(Guid equipmentId, CancellationToken cancellationToken = default)
    {
        var equipment = await equipmentRepository.GetByIdAsync(equipmentId, tracking: false, cancellationToken);
        return equipment is null ? null : Map(equipment);
    }

    public async Task<EquipmentResponse> UpdateAsync(Guid equipmentId, SaveEquipmentRequest request, CancellationToken cancellationToken = default)
    {
        var equipment = await equipmentRepository.GetByIdAsync(equipmentId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Equipment was not found.");
        if (equipment.ClientId != request.ClientId) throw new InvalidOperationException("Equipment cannot be reassigned to another client.");
        equipment.UpdateDetails(
            request.Name, request.Category, request.Manufacturer, request.Model, request.SerialNumber, request.Capacity,
            request.ItemType, request.LocationInsideClientLocation, request.Notes);
        equipment.UpdateMetadata(request.Labels, request.Dates, request.Checklist, request.Attachments);
        await equipmentRepository.SaveAsync(cancellationToken);
        return Map(equipment);
    }

    public async Task RecordInspectionAsync(Guid equipmentId, RecordEquipmentInspectionRequest request, CancellationToken cancellationToken = default)
    {
        var equipment = await equipmentRepository.GetByIdAsync(equipmentId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Equipment was not found.");
        equipment.RecordInspection(request.InspectedAt, request.NextDueAt);
        await equipmentRepository.SaveAsync(cancellationToken);
    }

    public async Task ArchiveAsync(Guid equipmentId, CancellationToken cancellationToken = default)
    {
        var equipment = await equipmentRepository.GetByIdAsync(equipmentId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Equipment was not found.");
        equipment.Archive();
        await equipmentRepository.SaveAsync(cancellationToken);
    }

    private static EquipmentResponse Map(Item equipment) => new(
        equipment.Id, equipment.ClientId, equipment.Name, equipment.Category, equipment.Manufacturer,
        equipment.Model, equipment.SerialNumber, equipment.Capacity, equipment.ItemType,
        equipment.LocationInsideClientLocation, equipment.Notes, equipment.GetLabels(), equipment.GetDates(),
        equipment.GetChecklist(), equipment.GetAttachments(), equipment.CertificateExpiryDate,
        equipment.LastInspectedOn, equipment.LastInspectionAt, equipment.NextInspectionDueAt, equipment.ArchivedAt);
}