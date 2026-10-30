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
    /// <summary>
    /// List the accepted quotations in one queue, chosen with <c>status</c>, newest activity first.
    /// </summary>
    /// <remarks>
    /// <c>status</c> values:
    /// - <c>unassigned</c>: accepted, waiting for the coordinator to give the job to an inspector (request status <c>unassigned</c>).
    /// - <c>assigned</c>: given to an inspector whose inspection is not finished (request status <c>assigned</c>, <c>task_accepted</c> or <c>in_progress</c>).
    /// - <c>certify</c>: inspection done, the certificate is being prepared, reviewed or sent (request status <c>certificate_submitted</c>, <c>certificate_returned</c>, <c>certificate_approved</c> or <c>sent_to_client</c>).
    /// Leave <c>status</c> out to get all of them; every row says which queue it is in with <c>queue</c>. A coordinator
    /// sees their own email requests and the team's manual ones; a manager sees their team; Admin sees everything (narrow
    /// it with <c>managerId</c>). An inspector only gets their own jobs, without prices, so <c>unassigned</c> is always empty
    /// for them. <c>inspectorId</c> narrows to the jobs of one inspector. <c>q</c> searches client, quotation number and
    /// location. <c>pageSize</c> defaults to 50 (max 100). Every row has <c>requestId</c> for the next calls.
    /// </remarks>
    /// <param name="status">Which queue: <c>unassigned</c>, <c>assigned</c> or <c>certify</c>. Leave out for all.</param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<QuoteSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<QuoteSummaryResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] Guid? managerId,
        [FromQuery] Guid? inspectorId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = QuoteListQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var role = User.FindFirst("role")?.Value ?? string.Empty;
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var actorId)) return Unauthorized();
        QuotationQueue? queue = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<QuotationQueue>(status.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                return BadRequest(new ProblemDetails { Title = "status must be 'unassigned', 'assigned' or 'certify'." });
            queue = parsed;
        }

        var query = new QuotationQueueQuery(managerId, inspectorId, q, page, pageSize);
        return Ok(await quoteService.ListQueueAsync(actorId, role, queue, query, cancellationToken));
    }

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
}

/// <summary>The new date of an inspection.</summary>
public sealed record ChangeInspectionDateRequest([property: Required] DateTimeOffset ScheduledAt, string? Note);
