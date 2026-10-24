using System.IdentityModel.Tokens.Jwt;
using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin,coordinator")]
[Route("api/v1/requests/{requestId:guid}/quotes")]
public sealed class QuotesController(IQuoteService quoteService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<QuoteResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<QuoteResponse>>> List(Guid requestId, CancellationToken cancellationToken) =>
        Ok(await quoteService.ListAsync(requestId, cancellationToken));

    [HttpPost]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<QuoteResponse>> Create(Guid requestId, CreateQuoteRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var quote = await quoteService.CreateAsync(requestId, actorId, request, cancellationToken);
        return CreatedAtAction(nameof(List), new { requestId }, quote);
    }

    [HttpPost("{quoteId:guid}/send")]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteResponse>> Send(Guid requestId, Guid quoteId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        return Ok(await quoteService.SendAsync(requestId, quoteId, actorId, cancellationToken));
    }

    [HttpPost("{quoteId:guid}/decision")]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<QuoteResponse>> Decision(Guid requestId, Guid quoteId, QuoteDecisionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        return Ok(await quoteService.RecordDecisionAsync(requestId, quoteId, actorId, request, cancellationToken));
    }

    private bool TryGetActor(out Guid actorId) => Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
}