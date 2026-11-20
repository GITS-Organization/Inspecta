using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Authorization;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>
/// Report files the inspector attaches to a job (scanned reports, photos, spreadsheets), reviewed by the technical manager. They are not part of
/// the certificates and are never reachable from the public check page; they are deleted 30 days after the job is delivered.
/// </summary>
[ApiController]
[Route("api/v1/quotation-queue/{requestId:guid}/reports")]
public sealed class JobReportsController(IJobReportService reports, ILogger<JobReportsController> logger) : ControllerBase
{
    /// <summary>The report files of a job, with the limits and whether the caller can add or remove files now.</summary>
    /// <remarks>
    /// The inspector of the job, technical managers and Admin always; the coordinator that owns the job from approval
    /// (<c>409 not_approved</c> before). A job the caller cannot see answers <c>404</c>.
    /// </remarks>
    [RequirePermission(Permissions.CertificatesView)]
    [HttpGet]
    [ProducesResponseType<JobReportListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobReportListResponse>> List(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await reports.ListAsync(requestId, actorId, role, cancellationToken));
    }

    /// <summary>Add one report file to the job.</summary>
    /// <remarks>
    /// Body: <c>fileName</c> (with its extension), <c>contentBase64</c> and, optionally, <c>requestItemId</c> (a machine of this job). One file per call.
    /// Allowed: pdf, png, jpg, jpeg, gif, txt, csv, doc, docx, xls, xlsx, ppt, pptx (no zip, no HEIC), 10 MB a file, 20 files and 30 MB a job.
    /// The assigned inspector, technical managers and Admin can add files while the inspection is in progress or returned
    /// (<c>409 reports_locked</c> otherwise); a coordinator gets <c>403 forbidden</c>. <c>409 report_limit_reached</c> when the job is full.
    /// </remarks>
    [RequirePermission(Permissions.InspectionWork)]
    [HttpPost]
    [ProducesResponseType<JobReportFileResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobReportFileResponse>> Upload(Guid requestId, UploadJobReportRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var file = await reports.UploadAsync(requestId, actorId, role, request, cancellationToken);
        return Created(file.DownloadUrl, file);
    }

    /// <summary>Download one report file (always as an attachment).</summary>
    [RequirePermission(Permissions.CertificatesView)]
    [HttpGet("{fileId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Download(Guid requestId, Guid fileId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var file = await reports.DownloadAsync(requestId, fileId, actorId, role, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Download every report of the job in one ZIP (always as an attachment).</summary>
    /// <remarks>
    /// The files are in the ZIP with their own names (<c>Weld report.pdf</c>, then <c>Weld report (2).pdf</c> for a second file with the same name).
    /// Same access as the list: the inspector of the job, technical managers and Admin; the owning coordinator from approval (<c>409 not_approved</c>
    /// before). <c>404</c> when the job has no report left (none added, or all expired). This is how the coordinator gets the reports after the
    /// job is delivered, until they are deleted 30 days later.
    /// </remarks>
    [RequirePermission(Permissions.CertificatesView)]
    [HttpGet("~/api/v1/quotation-queue/{requestId:guid}/reports.zip")]
    [Produces("application/zip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DownloadAll(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var file = await reports.DownloadAllAsync(requestId, actorId, role, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Remove one report file and its bytes for good.</summary>
    /// <remarks>
    /// The uploader while the inspection is in progress or returned (<c>409 reports_locked</c> after the submit); the job's reviewer or Admin any time
    /// until the files expire (<c>403 not_the_reviewer</c> for another technical manager). The answer is the new list. A removal by the technical
    /// manager is only written to the log.
    /// </remarks>
    [RequirePermission(Permissions.InspectionWork)]
    [HttpDelete("{fileId:guid}")]
    [ProducesResponseType<JobReportListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobReportListResponse>> Delete(Guid requestId, Guid fileId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var list = await reports.DeleteAsync(requestId, fileId, actorId, role, cancellationToken);
        logger.LogInformation("Report file {FileId} of job {RequestId} was removed by user {ActorId} ({Role}).", fileId, requestId, actorId, role);
        return Ok(list);
    }

    private bool TryGetActor(out Guid actorId, out string role)
    {
        role = User.FindFirst("role")?.Value ?? string.Empty;
        return Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
    }
}
