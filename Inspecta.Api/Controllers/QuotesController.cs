using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>
/// Quotations of an inspection request. A quotation is <c>open</c> (being prepared, or sent and waiting for the
/// client), <c>accepted</c> or <c>rejected</c>. Email requests can only be quoted by the coordinator that owns them.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamOperations)]
[Route("api/v1/requests/{requestId:guid}/quotes")]
public sealed class QuotesController(IQuoteService quoteService) : ControllerBase
{
    /// <summary>List the quotations of a request, oldest version first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<QuoteResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<QuoteResponse>>> List(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await quoteService.ListAsync(requestId, actorId, role, cancellationToken));
    }

    /// <summary>Get one quotation with its priced items.</summary>
    [HttpGet("{quoteId:guid}")]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuoteResponse>> Get(Guid requestId, Guid quoteId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await quoteService.GetAsync(requestId, quoteId, actorId, role, cancellationToken));
    }

    /// <summary>
    /// Open a quotation on a request. The items are copied from the request with empty prices; fill them in with
    /// <c>PUT</c>. The request moves to <c>quote_draft</c> and must have its client, location and items completed.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuoteResponse>> Create(Guid requestId, CreateQuoteRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var quote = await quoteService.CreateAsync(requestId, actorId, role, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { requestId, quoteId = quote.Id }, quote);
    }

    /// <summary>
    /// Save the draft: a price for every item of the request (each exactly once, prices may stay empty),
    /// validity, scope of work, notes and message. Only while the quotation is open and not sent.
    /// </summary>
    [HttpPut("{quoteId:guid}")]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuoteResponse>> Update(Guid requestId, Guid quoteId, UpdateQuoteRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await quoteService.UpdateAsync(requestId, quoteId, actorId, role, request, cancellationToken));
    }

    /// <summary>Preview the quotation as the PDF the client would receive.</summary>
    [HttpGet("{quoteId:guid}/pdf")]
    [Produces(PdfFile.ContentType)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pdf(Guid requestId, Guid quoteId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var file = await quoteService.GetPdfAsync(requestId, quoteId, actorId, role, cancellationToken);
        return File(file.Content, PdfFile.ContentType, file.FileName);
    }

    /// <summary>
    /// Reply with the offer (a first offer or a counter offer). The quotation PDF is generated and emailed, as a reply,
    /// to the sender of the original request using the coordinator mailbox. Only after the mail server accepted it
    /// is the quotation marked as sent and the request moved to <c>quote_sent</c>; if sending fails (<c>502</c>)
    /// nothing changes and it can be retried. Every item needs a price. Requests without a source email are only marked.
    /// </summary>
    [HttpPost("{quoteId:guid}/send")]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuoteResponse>> Send(Guid requestId, Guid quoteId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await quoteService.SendAsync(requestId, quoteId, actorId, role, cancellationToken));
    }

    /// <summary>
    /// Accept the request: the quotation is closed as <c>accepted</c> (request becomes <c>unassigned</c>, waiting for an inspector) and its PDF
    /// is emailed, as a reply, to the sender of the original request. Works on a draft (it is sent at the same time,
    /// so every item needs a price) and on an already sent quotation. A failed email (<c>502</c>) changes nothing.
    /// </summary>
    [HttpPost("{quoteId:guid}/accept")]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<QuoteResponse>> Accept(Guid requestId, Guid quoteId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await quoteService.AcceptAsync(requestId, quoteId, actorId, role, cancellationToken));
    }

    /// <summary>
    /// Record the outcome of a sent quotation as <c>accepted</c> or <c>rejected</c> (a reason is required for a
    /// rejection) without sending any email, for example when the client answered by phone.
    /// </summary>
    [HttpPost("{quoteId:guid}/decision")]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuoteResponse>> Decision(Guid requestId, Guid quoteId, QuoteDecisionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await quoteService.RecordDecisionAsync(requestId, quoteId, actorId, role, request, cancellationToken));
    }

    private bool TryGetActor(out Guid actorId, out string role)
    {
        role = User.FindFirst("role")?.Value ?? string.Empty;
        return Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
    }
}
