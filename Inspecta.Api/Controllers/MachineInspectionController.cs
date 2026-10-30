using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>
/// The work on an assigned job: the inspector inspects each machine and submits its certificate, the technical manager
/// approves or returns it, and the coordinator hands the approved certificates to the client.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamAccess)]
[Route("api/v1/quotation-queue/{requestId:guid}")]
public sealed class MachineInspectionController(IMachineInspectionService inspectionService) : ControllerBase
{
    /// <summary>Start the inspection of an assigned job and get its machines.</summary>
    /// <remarks>
    /// Accepts the task if the inspector has not yet, and moves the job to <c>in_progress</c>. Calling it again is safe.
    /// The assigned inspector (or the technical manager or Admin) can do it.
    /// </remarks>
    [HttpPost("start-inspection")]
    [ProducesResponseType<MachineListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MachineListResponse>> StartInspection(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await inspectionService.StartInspectionAsync(requestId, actorId, role, cancellationToken));
    }

    /// <summary>List the machines of a job with the state of each certificate.</summary>
    [HttpGet("machines")]
    [ProducesResponseType<MachineListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MachineListResponse>> ListMachines(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await inspectionService.ListMachinesAsync(requestId, actorId, role, cancellationToken));
    }

    /// <summary>Open one machine: everything the inspector's screen needs in one call.</summary>
    [HttpGet("machines/{requestItemId:guid}")]
    [ProducesResponseType<MachineFormResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MachineFormResponse>> OpenMachine(Guid requestId, Guid requestItemId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await inspectionService.GetFormAsync(requestId, requestItemId, actorId, role, cancellationToken));
    }

    /// <summary>Save what the inspector has entered for a machine (as often as needed).</summary>
    [HttpPut("machines/{requestItemId:guid}")]
    [ProducesResponseType<MachineFormResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MachineFormResponse>> SaveMachine(
        Guid requestId, Guid requestItemId, SaveMachineFormRequest form, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await inspectionService.SaveFormAsync(requestId, requestItemId, actorId, role, form, cancellationToken));
    }

    /// <summary>Submit a machine's certificate to the technical manager.</summary>
    [HttpPost("machines/{requestItemId:guid}/submit")]
    [ProducesResponseType<MachineFormResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MachineFormResponse>> SubmitMachine(Guid requestId, Guid requestItemId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await inspectionService.SubmitAsync(requestId, requestItemId, actorId, role, cancellationToken));
    }

    /// <summary>Approve a submitted machine certificate (technical manager).</summary>
    /// <remarks>Optional body: <c>signature</c> (the drawn strokes) and <c>rememberSignature</c>. Without a signature the one saved earlier is used.</remarks>
    [HttpPost("machines/{requestItemId:guid}/approve")]
    [Authorize(Policy = AuthorizationPolicies.TenantManagement)]
    [ProducesResponseType<MachineFormResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MachineFormResponse>> ApproveMachine(
        Guid requestId, Guid requestItemId, ApproveMachineRequest? request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await inspectionService.ApproveAsync(requestId, requestItemId, actorId, role, request, cancellationToken));
    }

    /// <summary>Send a submitted machine certificate back to the inspector with a note (technical manager).</summary>
    [HttpPost("machines/{requestItemId:guid}/return")]
    [Authorize(Policy = AuthorizationPolicies.TenantManagement)]
    [ProducesResponseType<MachineFormResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MachineFormResponse>> ReturnMachine(
        Guid requestId, Guid requestItemId, ReturnMachineRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await inspectionService.ReturnAsync(requestId, requestItemId, actorId, role, request.Note, cancellationToken));
    }

    /// <summary>Approve every machine of the job that is waiting for review (technical manager).</summary>
    /// <remarks>
    /// Same as approving each machine: every certificate gets its number, QR code and final PDF, filed under the client. Optional body:
    /// <c>signature</c> and <c>rememberSignature</c>. When every machine is approved the job is <c>certificate_approved</c> and reaches the coordinator.
    /// </remarks>
    [HttpPost("approve-all")]
    [Authorize(Policy = AuthorizationPolicies.TenantManagement)]
    [ProducesResponseType<MachineListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MachineListResponse>> ApproveAll(
        Guid requestId, ApproveMachineRequest? request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await inspectionService.ApproveAllAsync(requestId, actorId, role, request, cancellationToken));
    }

    /// <summary>Send the job back to the inspector with a note (technical manager).</summary>
    /// <remarks>
    /// Every machine waiting for review is sent back, or only those in <c>requestItemIds</c>; machines already approved stay approved.
    /// The job becomes <c>certificate_returned</c>, the inspector fixes the machines and submits them again.
    /// </remarks>
    [HttpPost("return-to-inspector")]
    [Authorize(Policy = AuthorizationPolicies.TenantManagement)]
    [ProducesResponseType<MachineListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MachineListResponse>> ReturnToInspector(
        Guid requestId, ReturnJobRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await inspectionService.ReturnToInspectorAsync(requestId, actorId, role, request, cancellationToken));
    }

    /// <summary>Download the QR code of an approved machine certificate as a PNG.</summary>
    /// <remarks>The QR printed on the approved certificate, as an image to send to the client. It opens the public check page with the PDF download.</remarks>
    [HttpGet("machines/{requestItemId:guid}/qr.png")]
    [Produces("image/png")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DownloadMachineQrCode(Guid requestId, Guid requestItemId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var file = await inspectionService.GetQrCodeAsync(requestId, requestItemId, actorId, role, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Download a machine's certificate as PDF.</summary>
    /// <remarks>
    /// Marked DRAFT until it is approved. The inspector sees it at any time, the technical manager and Admin too, and the
    /// coordinator once it is approved.
    /// </remarks>
    [HttpGet("machines/{requestItemId:guid}/pdf")]
    [Produces(PdfFile.ContentType)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DownloadMachinePdf(Guid requestId, Guid requestItemId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var file = await inspectionService.GetPdfAsync(requestId, requestItemId, actorId, role, cancellationToken);
        return File(file.Content, PdfFile.ContentType, file.FileName);
    }

    /// <summary>See the email that would carry the certificates to the client.</summary>
    /// <remarks>
    /// Returns the recipient (the sender of the original request first, then the client's certificates email), the subject, the suggested
    /// text and the attachments, so the coordinator can check and edit the text before handing the certificates over.
    /// </remarks>
    [HttpGet("deliver-to-client/preview")]
    [Authorize(Policy = AuthorizationPolicies.TeamOperations)]
    [ProducesResponseType<DeliveryPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeliveryPreviewResponse>> PreviewDelivery(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await inspectionService.PreviewDeliveryAsync(requestId, actorId, role, cancellationToken));
    }

    /// <summary>Download every certificate of the job in one ZIP.</summary>
    [HttpGet("certificates.zip")]
    [Authorize(Policy = AuthorizationPolicies.TeamOperations)]
    [Produces("application/zip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DownloadCertificatesZip(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var file = await inspectionService.GetCertificatesZipAsync(requestId, actorId, role, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>The coordinator hands the approved certificates to the client.</summary>
    /// <remarks>
    /// Possible once every machine of the job is approved. Marks the certificates as delivered and the job as
    /// <c>sent_to_client</c>. The owning coordinator, the technical manager or Admin can do it. When email delivery is on, the
    /// certificates are also emailed as a reply to the original request (see the preview); if the mail server refuses, the answer is
    /// <c>502</c> and nothing changes. The answer's <c>delivery</c> says whether an email went out.
    /// </remarks>
    [HttpPost("deliver-to-client")]
    [Authorize(Policy = AuthorizationPolicies.TeamOperations)]
    [ProducesResponseType<MachineListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MachineListResponse>> DeliverToClient(
        Guid requestId, DeliverCertificatesRequest? request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await inspectionService.DeliverAsync(requestId, actorId, role, request ?? new DeliverCertificatesRequest(), cancellationToken));
    }

    private bool TryGetActor(out Guid actorId, out string role)
    {
        role = User.FindFirst("role")?.Value ?? string.Empty;
        return Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
    }
}
