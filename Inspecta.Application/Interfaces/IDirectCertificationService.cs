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

    /// <summary>The machine kinds the catalogue knows.</summary>
    IReadOnlyList<MachineTypeResponse> MachineTypes();
}
