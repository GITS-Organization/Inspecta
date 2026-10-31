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

    /// <summary>Approve the whole job (technical manager).</summary>
    /// <remarks>
    /// Possible once every machine is submitted (<c>certificate_submitted</c>); a job is never approved in part. Every certificate gets
    /// its number and its final PDF (with the QR made on submit), filed under the client, whether it says Satisfactory or
    /// Unsatisfactory. Optional body: <c>signature</c> and <c>rememberSignature</c>. The job becomes <c>certificate_approved</c> and
    /// reaches the coordinator that owns it.
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

    /// <summary>Send the whole job back to the inspector with comments (technical manager).</summary>
    /// <remarks>
    /// Every machine is reopened with the general <c>note</c>; <c>machines</c> adds a comment on single machines (a wrong serial,
    /// a spelling mistake...). Every comment is kept in the job's <c>reviewHistory</c>. The job becomes <c>certificate_returned</c>;
    /// the inspector fixes the machines and submits them all again.
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

    /// <summary>Preview every certificate of the job in one ZIP, before approving (technical manager, inspector).</summary>
    /// <remarks>
    /// One PDF per machine that has been saved, exactly as it will be printed: marked DRAFT with the QR box empty until it is
    /// approved, the stored final file after. The technical manager, Admin and the job's inspector can download it; the coordinator
    /// uses <c>certificates.zip</c> once the job is approved (<c>403</c> here).
    /// </remarks>
    [HttpGet("certificates/preview.zip")]
    [Produces("application/zip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewCertificatesZip(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var file = await inspectionService.GetPreviewZipAsync(requestId, actorId, role, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>The email that would carry the certificates to the client, ready to send.</summary>
    /// <remarks>
    /// The Deliver button opens this. It returns the recipient (the sender of the original request first, then the client's certificates
    /// email), <c>cc</c>, the subject (<c>Re: ...</c>, so it stays in the same thread), the suggested text, the certificates that go in it
    /// (each with its result, dates, <c>pdfUrl</c> to open it and its check link) and the attachments. The coordinator may change
    /// <c>to</c>, <c>cc</c>, <c>subject</c> and <c>body</c> and send them to <c>POST deliver-to-client</c>. <c>canSend</c> says whether
    /// Send works and <c>blockers</c> why not; <c>testEmailTo</c> is where <c>deliver-to-client/test-email</c> goes.
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

    /// <summary>The coordinator sends the certificates to the client (the Send button).</summary>
    /// <remarks>
    /// Possible once every machine of the job is approved. The body is optional: <c>sentTo</c>, <c>cc</c>, <c>subject</c> and <c>message</c>
    /// replace the suggestion of the preview. With email delivery on, the email goes out first (a reply to the original request, from
    /// the coordinator's mailbox, every PDF attached) and only then are the certificates marked delivered and the job <c>sent_to_client</c>:
    /// if the mail server refuses, the answer is <c>502</c> and nothing changes, so Send can be pressed again (never two emails). With
    /// no address to send to it answers <c>409 no_recipient</c>. With email delivery off it only marks the job as handed over. The owning
    /// coordinator, the technical manager or Admin can do it. The answer's <c>delivery</c> says what went out, to whom and when.
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

    /// <summary>Send a test copy of the email to yourself.</summary>
    /// <remarks>
    /// Sends the email as it would go out (with the same optional body as <c>deliver-to-client</c>: the coordinator's edits, and every PDF) to
    /// the signed-in user's own address only, with <c>[TEST]</c> in the subject. Nothing is recorded and the job does not change. Answers
    /// <c>409 email_disabled</c> while email delivery is off.
    /// </remarks>
    [HttpPost("deliver-to-client/test-email")]
    [Authorize(Policy = AuthorizationPolicies.TeamOperations)]
    [ProducesResponseType<TestEmailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TestEmailResponse>> SendTestEmail(
        Guid requestId, DeliverCertificatesRequest? request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await inspectionService.SendTestDeliveryAsync(requestId, actorId, role, request ?? new DeliverCertificatesRequest(), cancellationToken));
    }

    /// <summary>The certificates were handed over another way: mark the job delivered without an email.</summary>
    /// <remarks>
    /// For certificates given on paper, by chat or picked up. Optional body: <c>deliveredTo</c> and a <c>note</c> kept in the job's history.
    /// Marks the certificates delivered and the job <c>sent_to_client</c>; no email is sent whatever the email setting.
    /// </remarks>
    [HttpPost("deliver-to-client/manual")]
    [Authorize(Policy = AuthorizationPolicies.TeamOperations)]
    [ProducesResponseType<MachineListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MachineListResponse>> DeliverManually(
        Guid requestId, ManualDeliveryRequest? request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await inspectionService.DeliverManuallyAsync(requestId, actorId, role, request ?? new ManualDeliveryRequest(), cancellationToken));
    }

    private bool TryGetActor(out Guid actorId, out string role)
    {
        role = User.FindFirst("role")?.Value ?? string.Empty;
        return Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
    }
}
