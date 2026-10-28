using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Create and manage quote revisions for an inspection request.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamOperations)]
[Route("api/v1/requests/{requestId:guid}/quotes")]
public sealed class QuotesController(IQuoteService quoteService) : ControllerBase
{
    /// <summary>List quote revisions for a request.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<QuoteResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<QuoteResponse>>> List(Guid requestId, CancellationToken cancellationToken) =>
        Ok(await quoteService.ListAsync(requestId, cancellationToken));

    /// <summary>Create a quote revision.</summary>
    [HttpPost]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<QuoteResponse>> Create(Guid requestId, CreateQuoteRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var quote = await quoteService.CreateAsync(requestId, actorId, request, cancellationToken);
        return CreatedAtAction(nameof(List), new { requestId }, quote);
    }

    /// <summary>Send a draft quote to the client.</summary>
    [HttpPost("{quoteId:guid}/send")]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteResponse>> Send(Guid requestId, Guid quoteId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        return Ok(await quoteService.SendAsync(requestId, quoteId, actorId, cancellationToken));
    }

    /// <summary>Record the client's decision on a quote.</summary>
    [HttpPost("{quoteId:guid}/decision")]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteResponse>> Decision(Guid requestId, Guid quoteId, QuoteDecisionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        return Ok(await quoteService.RecordDecisionAsync(requestId, quoteId, actorId, request, cancellationToken));
    }

    private bool TryGetActor(out Guid actorId) => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
}