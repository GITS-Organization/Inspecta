using Inspecta.Application.Certificates;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;

namespace Inspecta.Application.Services;

public sealed class DirectCertificationService(
    IInspectionRequestService requests,
    IRequestRepository requestRepository,
    IClientRepository clients,
    IMachineInspectionService machines,
    IRequestEquipmentRegistrar? equipmentRegistrar = null) : IDirectCertificationService
{
    public async Task<MachineListResponse> CreateAsync(
        CreateCertificationRequest request, Guid actorId, string role, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        if (role is not ("admin" or "technical_manager" or "inspector"))
            throw new AuthException("forbidden", "Only an inspector, the technical manager or the admin can create a certificate directly.", 403);
        if (request.Machines.Count == 0) throw new ArgumentException("Pick or add at least one machine.");

        var client = await clients.GetByIdAsync(request.ClientId, tracking: false, cancellationToken)
            ?? throw new KeyNotFoundException("Client was not found.");
        if (client.ArchivedAt is not null) throw new KeyNotFoundException("Client was not found.");

        var managerId = role == "admin" ? request.ManagerId ?? client.ManagerId : tenantId;
        var created = await requests.CreateAsync(
            new CreateInspectionRequest
            {
                ClientId = client.Id,
                ClientName = client.Name,
                Location = string.IsNullOrWhiteSpace(request.Location) ? client.Location ?? client.Name : request.Location,
                Subject = "Direct certificate",
                Items = request.Machines
            },
            actorId,
            managerId,
            cancellationToken);

        var job = await requestRepository.GetByIdAsync(created.Id, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Request was not found.");
        // New machines are kept for the client (gears are not); this has to happen before the job leaves the details stage.
        if (equipmentRegistrar is not null) await equipmentRegistrar.RegisterAsync(job, cancellationToken);
        job.BeginDirectInspection(actorId);
        await requestRepository.SaveAsync(job, cancellationToken);
        return await machines.ListMachinesAsync(created.Id, actorId, role, cancellationToken);
    }

    public IReadOnlyList<MachineTypeResponse> MachineTypes() =>
        CertificateTemplateCatalog.Items
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new MachineTypeResponse(
                item.Name,
                item.Aliases,
                item.Standards,
                item.Template,
                CertificateTemplateCatalog.Find(item.Template)?.Name ?? item.Template,
                item.Gear))
            .ToArray();
}
