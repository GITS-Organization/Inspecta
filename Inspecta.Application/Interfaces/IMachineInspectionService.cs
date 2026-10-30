using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Pdf;

namespace Inspecta.Application.Interfaces;

/// <summary>
/// The work after a quotation is assigned: the inspector inspects each machine of the job and submits its certificate,
/// the technical manager approves or returns it, and the coordinator hands the approved certificates to the client.
/// </summary>
public interface IMachineInspectionService
{
    /// <summary>Accepts the task if needed and starts the inspection; returns the machines to inspect.</summary>
    Task<MachineListResponse> StartInspectionAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<MachineListResponse> ListMachinesAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<MachineFormResponse> GetFormAsync(Guid requestId, Guid requestItemId, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<MachineFormResponse> SaveFormAsync(Guid requestId, Guid requestItemId, Guid actorId, string role, SaveMachineFormRequest form, CancellationToken cancellationToken = default);

    Task<MachineFormResponse> SubmitAsync(Guid requestId, Guid requestItemId, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<MachineFormResponse> ApproveAsync(Guid requestId, Guid requestItemId, Guid actorId, string role, ApproveMachineRequest? options = null, CancellationToken cancellationToken = default);

    Task<MachineFormResponse> ReturnAsync(Guid requestId, Guid requestItemId, Guid actorId, string role, string note, CancellationToken cancellationToken = default);

    Task<PdfFile> GetPdfAsync(Guid requestId, Guid requestItemId, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<MachineListResponse> DeliverAsync(Guid requestId, Guid actorId, string role, DeliverCertificatesRequest request, CancellationToken cancellationToken = default);

    Task<DeliveryPreviewResponse> PreviewDeliveryAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<DownloadFile> GetCertificatesZipAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);
}
