using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Accepted quotations, grouped by what has to happen to them next.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamAccess)]
[Route("api/v1/quotation-queue")]
public sealed class QuotationQueueController(IQuoteService quoteService, IInspectionRequestService requestService) : ControllerBase
{
    /// <summary>Accepted quotations that are waiting for the coordinator to give them to an inspector.</summary>
    /// <remarks>
    /// Every row has <c>requestId</c>: pick an inspector with <c>GET /inspectors</c> and give it the job with
    /// <c>POST /requests/{requestId}/assignment</c>. A coordinator sees their own email requests and the team's manual
    /// ones; a manager sees their team; Admin sees everything (narrow it with <c>managerId</c>). Inspectors get an empty
    /// list here. <c>pageSize</c> defaults to 50 (max 100).
    /// </remarks>
    [HttpGet("unassigned")]
    [ProducesResponseType<IReadOnlyList<QuoteSummaryResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<QuoteSummaryResponse>>> ListUnassigned(
        [FromQuery] Guid? managerId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = QuoteListQuery.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        ListQueueAsync(QuotationQueue.Unassigned, new QuotationQueueQuery(managerId, null, q, page, pageSize), cancellationToken);

    /// <summary>Quotations given to an inspector whose inspection has not been finished yet.</summary>
    /// <remarks>
    /// An inspector gets their own jobs here, with the machines and the company but no prices, and can move the date with
    /// <c>PUT /quotation-queue/{requestId}/inspection-date</c>. The coordinator keeps full access and can see who got each
    /// job (<c>inspectorName</c>, <c>scheduledAt</c>) or filter by <c>inspectorId</c>.
    /// </remarks>
    [HttpGet("assigned")]
    [ProducesResponseType<IReadOnlyList<QuoteSummaryResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<QuoteSummaryResponse>>> ListAssigned(
        [FromQuery] Guid? managerId,
        [FromQuery] Guid? inspectorId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = QuoteListQuery.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        ListQueueAsync(QuotationQueue.Assigned, new QuotationQueueQuery(managerId, inspectorId, q, page, pageSize), cancellationToken);

    /// <summary>Quotations whose inspection is done and whose certificate is being prepared, reviewed or sent.</summary>
    [HttpGet("certify")]
    [ProducesResponseType<IReadOnlyList<QuoteSummaryResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<QuoteSummaryResponse>>> ListCertify(
        [FromQuery] Guid? managerId,
        [FromQuery] Guid? inspectorId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = QuoteListQuery.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        ListQueueAsync(QuotationQueue.Certify, new QuotationQueueQuery(managerId, inspectorId, q, page, pageSize), cancellationToken);

    /// <summary>Move the inspection to another date. The only change an inspector can make to a job.</summary>
    /// <remarks>
    /// Allowed while the job is <c>assigned</c> or <c>task_accepted</c>, to the inspector of the job, the coordinator that
    /// owns the request, a manager or Admin. The change is written to the request history. Other inspectors get
    /// <c>404</c>; a job that already started gets <c>409</c>; a date inside the inspector's leave is refused for the
    /// coordinator side.
    /// </remarks>
    [HttpPut("{requestId:guid}/inspection-date")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestResponse>> ChangeInspectionDate(
        Guid requestId,
        ChangeInspectionDateRequest request,
        CancellationToken cancellationToken)
    {
        var role = User.FindFirst("role")?.Value ?? string.Empty;
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var actorId)) return Unauthorized();
        return Ok(await requestService.ChangeInspectionDateAsync(
            requestId, actorId, role, request.ScheduledAt, request.Note, cancellationToken));
    }

    private async Task<ActionResult<IReadOnlyList<QuoteSummaryResponse>>> ListQueueAsync(
        QuotationQueue queue,
        QuotationQueueQuery query,
        CancellationToken cancellationToken)
    {
        var role = User.FindFirst("role")?.Value ?? string.Empty;
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var actorId)) return Unauthorized();
        return Ok(await quoteService.ListQueueAsync(actorId, role, queue, query, cancellationToken));
    }
}

/// <summary>The new date of an inspection.</summary>
public sealed record ChangeInspectionDateRequest([property: Required] DateTimeOffset ScheduledAt, string? Note);
