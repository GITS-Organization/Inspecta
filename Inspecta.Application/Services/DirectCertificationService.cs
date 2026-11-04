using Inspecta.Application.Certificates;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

public sealed class DirectCertificationService(
    IInspectionRequestService requests,
    IRequestRepository requestRepository,
    IClientRepository clients,
    IMachineInspectionService machines,
    IRequestEquipmentRegistrar? equipmentRegistrar = null,
    IEquipmentService? equipment = null,
    IQuoteRepository? quoteRepository = null,
    ITransactionRunner? transactions = null) : IDirectCertificationService
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
        if (role == "inspector" && !(await InspectorClientIdsAsync(actorId, cancellationToken)).Contains(client.Id))
            throw new AuthException("forbidden", "An inspector can create certificates only for the clients of the jobs assigned to them.", 403);

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
        async Task<bool> Save()
        {
            await requestRepository.SaveAsync(job, cancellationToken);
            // The placeholder quotation puts the job in the quotation queue and in the technical manager's review list.
            if (quoteRepository is not null)
                await quoteRepository.AddAsync(Quote.CreateAccepted(job.Id, actorId, "DC", job.ClientName, "Direct certificate"), cancellationToken);
            return true;
        }

        if (transactions is null) await Save();
        else await transactions.RunAsync(Save, cancellationToken);
        return await machines.ListMachinesAsync(created.Id, actorId, role, cancellationToken);
    }

    // The clients of the jobs an inspector still has in hand are the ones they may certify directly.
    private async Task<HashSet<Guid>> InspectorClientIdsAsync(Guid inspectorId, CancellationToken cancellationToken) =>
        (await requestRepository.ListClientIdsInHandAsync(inspectorId, cancellationToken)).ToHashSet();

    public async Task<IReadOnlyList<CertificationClientResponse>> ClientsAsync(
        Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        if (role is not ("admin" or "technical_manager" or "inspector"))
            throw new AuthException("forbidden", "Only an inspector, the technical manager or the admin can create a certificate directly.", 403);

        var all = await clients.ListAsync(includeArchived: false, cancellationToken);
        if (role == "inspector")
        {
            var mine = await InspectorClientIdsAsync(actorId, cancellationToken);
            all = all.Where(client => mine.Contains(client.Id)).ToList();
        }

        return all.OrderBy(client => client.Name, StringComparer.OrdinalIgnoreCase)
            .Select(client => new CertificationClientResponse(client.Id, client.Name, client.Location))
            .ToArray();
    }

    public async Task<IReadOnlyList<Contracts.Equipment.EquipmentResponse>> MachinesOfAsync(
        Guid clientId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        if ((await ClientsAsync(actorId, role, cancellationToken)).All(client => client.Id != clientId))
            throw new KeyNotFoundException("Client was not found.");
        return equipment is null ? [] : await equipment.ListAsync(clientId, includeArchived: false, cancellationToken);
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
