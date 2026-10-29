using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>The quotations the current user can see, across all requests.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamOperations)]
[Route("api/v1/quotations")]
public sealed class QuotationsController(IQuoteService quoteService) : ControllerBase
{
    /// <summary>
    /// List quotations, newest activity first. A coordinator sees the quotations of their own email requests and of the
    /// team's manual requests; managers see their team, Admin sees everything.
    /// </summary>
    /// <remarks>
    /// <c>stage</c> is <c>draft</c> (still being prepared), <c>sent</c> (waiting for the client), <c>accepted</c> or
    /// <c>rejected</c>; leave it out for all. Only the latest revision of each request is returned unless
    /// <c>includeSuperseded=true</c>. <c>q</c> searches client, quotation number and location. <c>pageSize</c> defaults
    /// to 50 (max 100). Each row has <c>requestId</c> and <c>id</c>: open it with
    /// <c>GET /requests/{requestId}/quotes/{id}</c> and work on it with <c>PUT</c> there while <c>canEdit</c> is true.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<QuoteSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<QuoteSummaryResponse>>> List(
        [FromQuery] Guid? managerId,
        [FromQuery] string? stage,
        [FromQuery] string? q,
        [FromQuery] bool includeSuperseded = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = QuoteListQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var role = User.FindFirst("role")?.Value ?? string.Empty;
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var actorId)) return Unauthorized();
        var query = new QuoteListQuery(
            managerId,
            string.IsNullOrWhiteSpace(stage) ? null : ParseStage(stage),
            q,
            includeSuperseded,
            page,
            pageSize);
        return Ok(await quoteService.ListVisibleAsync(actorId, role, query, cancellationToken));
    }

    private static QuoteStage ParseStage(string value) =>
        Enum.TryParse<QuoteStage>(value, ignoreCase: true, out var stage) && Enum.IsDefined(stage)
            ? stage
            : throw new ArgumentException("stage must be 'draft', 'sent', 'accepted' or 'rejected'.");
}
