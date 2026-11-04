using Inspecta.Application.Certificates;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Contracts.Equipment;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

public sealed class JobMachineService(
    IRequestRepository requestRepository,
    ICertificateRepository certificateRepository,
    IEquipmentCatalog catalog,
    IRequestEquipmentRegistrar registrar,
    IEquipmentService equipment,
    IMachineInspectionService machines) : IJobMachineService
{
    public async Task<IReadOnlyList<EquipmentResponse>> AvailableAsync(
        Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, tracking: false, cancellationToken);
        if (request.ClientId is not Guid clientId) return [];
        var inJob = request.Items.Where(item => item.EquipmentId is not null).Select(item => item.EquipmentId!.Value).ToHashSet();
        return (await equipment.ListAsync(clientId, includeArchived: false, cancellationToken)).Where(item => !inJob.Contains(item.Id)).ToArray();
    }

    public async Task<MachineListResponse> AddAsync(
        Guid requestId, Guid actorId, string role, AddMachineRequest machine, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, tracking: true, cancellationToken);
        EnsureAllowed(request, actorId, role);

        RequestItem item;
        if (machine.EquipmentId is Guid equipmentId)
        {
            if (request.ClientId is not Guid clientId)
                throw new ArgumentException("The job has no client, so a registered machine cannot be picked.");
            var owners = await catalog.GetEquipmentOwnersAsync([equipmentId], cancellationToken);
            if (!owners.TryGetValue(equipmentId, out var owner) || owner != clientId)
                throw new ArgumentException("The selected machine does not belong to the client of this job.");
            var registered = (await catalog.ListClientEquipmentAsync(clientId, cancellationToken)).Single(value => value.Id == equipmentId);
            item = RequestItem.Create(
                registered.Name, GearRules.QuantityFor(registered.Name, machine.Quantity), registered.Id, machine.Type, machine.Capacity ?? registered.Capacity,
                machine.SerialNumber ?? registered.SerialNumber, registered.Category, registered.Manufacturer, registered.Model,
                registered.PrNumber, machine.Location ?? registered.LocationInsideClientLocation, machine.IssueDescription);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(machine.Name)) throw new ArgumentException("Give the machine's name or pick a registered machine.");
            item = RequestItem.Create(
                machine.Name, GearRules.QuantityFor(machine.Name, machine.Quantity), null, machine.Type, machine.Capacity, machine.SerialNumber, null,
                machine.Manufacturer, machine.Model, null, machine.Location, machine.IssueDescription);
            await registrar.RegisterMachinesAsync(request, [item], cancellationToken);
        }

        request.AddMachine(item, actorId);
        await requestRepository.SaveRevisionAsync(request, new RequestItemChanges([item], []), cancellationToken);
        return await machines.ListMachinesAsync(requestId, actorId, role, cancellationToken);
    }

    public async Task<MachineListResponse> EditAsync(
        Guid requestId, Guid requestItemId, Guid actorId, string role, EditMachineRequest machine, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, tracking: true, cancellationToken);
        EnsureAllowed(request, actorId, role);
        if (request.Items.All(item => item.Id != requestItemId)) throw new KeyNotFoundException("Machine was not found.");
        var certificate = await certificateRepository.GetByRequestItemIdAsync(requestId, requestItemId, tracking: false, cancellationToken);
        if (MachineInspectionService.StatusOf(certificate) is MachineStatus.Submitted or MachineStatus.Approved or MachineStatus.Delivered)
            throw new AuthException("certificate_locked", "A submitted certificate cannot be changed; wait for the review or for it to be returned.", 409);

        request.EditMachine(
            requestItemId, machine.Name, GearRules.QuantityFor(machine.Name, machine.Quantity), machine.Type, machine.Capacity, machine.SerialNumber,
            machine.Manufacturer, machine.Model, machine.Location, machine.IssueDescription, actorId);
        await requestRepository.SaveAsync(request, cancellationToken);
        return await machines.ListMachinesAsync(requestId, actorId, role, cancellationToken);
    }

    public async Task<MachineListResponse> DivideAsync(
        Guid requestId, Guid requestItemId, Guid actorId, string role, DivideMachineRequest division, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, tracking: true, cancellationToken);
        EnsureAllowed(request, actorId, role);
        var item = request.Items.SingleOrDefault(value => value.Id == requestItemId) ?? throw new KeyNotFoundException("Machine was not found.");
        if (CertificateTemplateCatalog.Resolve(item.Name).Item?.Gear != true)
            throw new AuthException("not_a_gear", "Only a gear can be divided into several certificates.", 409);

        var shares = ReadShares(division);
        var group = request.Items.Where(value => value.Id == item.Id || (item.DivisionGroupId is not null && value.DivisionGroupId == item.DivisionGroupId)).ToList();
        foreach (var member in group)
        {
            if (await certificateRepository.GetByRequestItemIdAsync(requestId, member.Id, tracking: false, cancellationToken) is not null)
                throw new AuthException("machine_already_started", "A certificate of this gear was already started, so it cannot be divided again.", 409);
        }

        var (added, removed) = request.DivideItem(requestItemId, shares, actorId);
        await requestRepository.SaveRevisionAsync(request, new RequestItemChanges(added, removed), cancellationToken);
        return await machines.ListMachinesAsync(requestId, actorId, role, cancellationToken);
    }

    private static List<(int Quantity, string Serials)> ReadShares(DivideMachineRequest division)
    {
        var shares = new List<(int, string)>();
        var certificates = division.Certificates ?? [];
        if (certificates.Count == 0) throw new ArgumentException("Choose how many certificates to make and write their serial numbers.");
        for (var index = 0; index < certificates.Count; index++)
        {
            var number = index + 1;
            var serials = (certificates[index].SerialNumbers ?? string.Empty).Trim();
            if (certificates[index].Quantity < 1) throw new ArgumentException($"Certificate {number}: the quantity must be at least 1.");
            if (serials.Length == 0) throw new ArgumentException($"Certificate {number}: write its serial numbers.");
            if (serials.Length > SerialNumbers.MaxCharacters)
                throw new ArgumentException(
                    $"Certificate {number}: the serial numbers are {serials.Length} characters and only {SerialNumbers.MaxCharacters} fit on one page. Make another certificate for the rest.");
            shares.Add((certificates[index].Quantity, serials));
        }

        return shares;
    }

    public async Task<MachineListResponse> RemoveAsync(
        Guid requestId, Guid requestItemId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, tracking: true, cancellationToken);
        EnsureAllowed(request, actorId, role);
        if (request.Items.All(item => item.Id != requestItemId)) throw new KeyNotFoundException("Machine was not found.");
        if (await certificateRepository.GetByRequestItemIdAsync(requestId, requestItemId, tracking: false, cancellationToken) is not null)
            throw new AuthException("machine_already_started", "This machine's certificate was already started, so it cannot be removed.", 409);

        var removed = request.RemoveMachine(requestItemId, actorId);
        await requestRepository.SaveRevisionAsync(request, new RequestItemChanges([], [removed]), cancellationToken);
        return await machines.ListMachinesAsync(requestId, actorId, role, cancellationToken);
    }

    private async Task<InspectionRequest> LoadAsync(Guid requestId, Guid actorId, string role, bool tracking, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role)) throw new KeyNotFoundException("Request was not found.");
        return request;
    }

    // The assigned inspector, the coordinator of the job, the technical manager and the admin can change its machines.
    private static void EnsureAllowed(InspectionRequest request, Guid actorId, string role)
    {
        var allowed = role switch
        {
            "admin" or "technical_manager" => true,
            "inspector" => request.InspectorId == actorId,
            "coordinator" => RequestAccess.CanManageQuotes(request, actorId, role),
            _ => false
        };
        if (!allowed)
            throw new AuthException("forbidden", "Only the inspector of the job, its coordinator, the technical manager or the admin can change its machines.", 403);
        if (!request.CanChangeMachines)
            throw new AuthException("job_not_open", "Machines can be changed from the assignment until the inspector submits the job.", 409);
    }
}
