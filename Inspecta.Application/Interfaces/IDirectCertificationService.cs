using Inspecta.Application.Contracts.Certificates;

namespace Inspecta.Application.Interfaces;

public interface IDirectCertificationService
{
    /// <summary>
    /// Creates a certificate job without a request or a quotation: the person who inspects picks the client and the machines and the job
    /// starts in progress, ready to be filled and sent to the technical manager. Returns the job's machines like <c>start-inspection</c>.
    /// </summary>
    Task<MachineListResponse> CreateAsync(
        CreateCertificationRequest request, Guid actorId, string role, Guid? tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The clients a certificate can be created for. The technical manager and the admin get every client of their team; an inspector gets
    /// only the clients of the jobs assigned to them that are not finished (assigned, accepted, in progress or returned).
    /// </summary>
    Task<IReadOnlyList<CertificationClientResponse>> ClientsAsync(Guid actorId, string role, CancellationToken cancellationToken = default);

    /// <summary>The registered machines of one of those clients; not found for any other client.</summary>
    Task<IReadOnlyList<Inspecta.Application.Contracts.Equipment.EquipmentResponse>> MachinesOfAsync(
        Guid clientId, Guid actorId, string role, CancellationToken cancellationToken = default);

    /// <summary>The machine kinds the catalogue knows.</summary>
    IReadOnlyList<MachineTypeResponse> MachineTypes();
}
