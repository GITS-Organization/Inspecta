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

    /// <summary>Approves every machine of the job that is waiting for review.</summary>
    Task<MachineListResponse> ApproveAllAsync(Guid requestId, Guid actorId, string role, ApproveMachineRequest? approval = null, CancellationToken cancellationToken = default);

    /// <summary>Sends the machines waiting for review (all, or the ones named) back to the inspector with a note.</summary>
    Task<MachineListResponse> ReturnToInspectorAsync(Guid requestId, Guid actorId, string role, ReturnJobRequest returned, CancellationToken cancellationToken = default);

    /// <summary>Every saved certificate of the job in one ZIP as it will be printed (drafts marked DRAFT), for review before approval.</summary>
    Task<DownloadFile> GetPreviewZipAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);

    /// <summary>The QR code of an approved certificate as a PNG.</summary>
    Task<DownloadFile> GetQrCodeAsync(Guid requestId, Guid requestItemId, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<PdfFile> GetPdfAsync(Guid requestId, Guid requestItemId, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<MachineListResponse> DeliverAsync(Guid requestId, Guid actorId, string role, DeliverCertificatesRequest request, CancellationToken cancellationToken = default);

    Task<DeliveryPreviewResponse> PreviewDeliveryAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<DownloadFile> GetCertificatesZipAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);
}
