using Inspecta.Application.Authorization;
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
[Route("api/v1/requests/{requestId:guid}/quotes")]
public sealed class QuotesController(IQuoteService quoteService) : ControllerBase
{
    /// <summary>List the quotations of a request, oldest version first.</summary>
    [RequirePermission(Permissions.QuotesView)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<QuoteResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<QuoteResponse>>> List(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await quoteService.ListAsync(requestId, actorId, role, cancellationToken));
    }

    /// <summary>Get one quotation with its priced items.</summary>
    [RequirePermission(Permissions.QuotesView)]
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
    [RequirePermission(Permissions.QuotesWork)]
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

    /// <summary>The client's reply needs nothing from the coordinator: clear it and keep waiting for the client.</summary>
    /// <remarks>
    /// When the client answers one of our quotation emails (it replies to it), the email does not become a new request: it is kept with the request, shown in
    /// <c>clientReplies</c> of the current quotation, and the request goes to <c>client_countered</c> (back in front of the coordinator). For an answer that
    /// changes nothing (a thank-you, a question already answered) this clears the replies, marks them read and puts the request back to <c>quote_sent</c>
    /// (<c>counter_offer_sent</c> for a revision). <c>409</c> when there is no reply to dismiss. Accepting, rejecting or sending the next offer clears the replies too.
    /// </remarks>
    [RequirePermission(Permissions.QuotesWork)]
    [HttpPost("{quoteId:guid}/dismiss-reply")]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuoteResponse>> DismissReply(Guid requestId, Guid quoteId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await quoteService.DismissClientReplyAsync(requestId, quoteId, actorId, role, cancellationToken));
    }

    /// <summary>
    /// Take a sent quotation back to the quotations being prepared, by hand: for example when the price was talked over by phone and the
    /// offer has to change before it is sent again.
    /// </summary>
    /// <remarks>
    /// Works on the current sent quotation while the client has not accepted or rejected it (the request is <c>quote_sent</c>, <c>counter_offer_sent</c>
    /// or <c>client_countered</c>). It opens a new revision (<c>rev02</c>, <c>rev03</c>...) that starts with the prices of the sent one, which stays
    /// as it was and is marked as replaced, and the request goes back to <c>quote_draft</c>. Edit it with <c>PUT</c> and send it with <c>send</c>:
    /// from the second revision on the request becomes <c>counter_offer_sent</c>. The same as <c>POST .../quotes</c> on a sent request, for the
    /// quotation on the screen. <c>409</c> when the quotation cannot be taken back (not sent, already answered, or already replaced).
    /// </remarks>
    [RequirePermission(Permissions.QuotesWork)]
    [HttpPost("{quoteId:guid}/reopen")]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuoteResponse>> Reopen(Guid requestId, Guid quoteId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var quote = await quoteService.ReopenAsync(requestId, quoteId, actorId, role, cancellationToken);
        return CreatedAtAction(nameof(Get), new { requestId, quoteId = quote.Id }, quote);
    }

    /// <summary>
    /// Save the draft: a price for every item of the request (each exactly once, prices may stay empty),
    /// validity, scope of work, notes and message. Only while the quotation is open and not sent.
    /// <c>discountPercent</c> (0 to 100) is taken off the total of all lines, before VAT: leave it out to keep the current one, send 0 to remove it.
    /// <c>vatPercent</c> (0 to 100, new quotations start with 0 = no VAT; the usual rate is 14) is added on the total after the discount: leave it out to keep the current one, send 0 to leave the VAT out. <c>amountPiastres</c> stays before VAT; the answer also has <c>vatPiastres</c> and <c>totalWithVatPiastres</c>.
    /// The answer has <c>subtotalPiastres</c>, <c>discountPercent</c>, <c>discountPiastres</c>, and <c>amountPiastres</c> is the total after the discount.
    /// </summary>
    [RequirePermission(Permissions.QuotesWork)]
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
    [RequirePermission(Permissions.QuotesView)]
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
    /// is the quotation marked as sent and the request moved to <c>quote_sent</c> (the first offer) or <c>counter_offer_sent</c> (a revision, the second or later); if sending fails (<c>502</c>)
    /// nothing changes and it can be retried. Every item needs a price. Requests without a source email are only marked, unless a
    /// <c>sentTo</c> is given.
    /// </summary>
    /// <remarks>
    /// The body is optional. To let the coordinator edit the email, call <c>GET {quoteId}/email?action=send</c> first and send back
    /// <c>sentTo</c>, <c>cc</c>, <c>subject</c> and <c>message</c>; a field that is left out keeps the suggestion. The PDF is never changed.
    /// The suggested subject says what is sent: <c>Quotation {number} - {client}</c>, <c>Revised Quotation ...</c> for a counter offer.
    /// <c>attachments</c> adds files of the coordinator (<c>[{ "fileName": "terms.pdf", "contentBase64": "..." }]</c>; at most 5, 15 MB together;
    /// pdf, images, Word, Excel, PowerPoint, txt, csv and zip; the content must match the type, or <c>400</c>). They go after the quotation PDF.
    /// If sending fails, send the files again with the retry.
    /// </remarks>
    [RequirePermission(Permissions.QuotesWork)]
    [HttpPost("{quoteId:guid}/send")]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<QuoteResponse>> Send(
        Guid requestId, Guid quoteId, [FromBody] SendQuoteEmailRequest? email, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await quoteService.SendAsync(requestId, quoteId, actorId, role, email, cancellationToken));
    }

    /// <summary>The email that would carry the quotation PDF, ready to be edited: to, cc, subject and text.</summary>
    /// <remarks>
    /// <c>action</c> is <c>send</c> (an offer or a counter offer, the default) or <c>accept</c>; the text differs. Nothing is sent. The
    /// answer has <c>canSend</c> and <c>blockers</c> (why Send would fail: a price missing, no mailbox, no address) and <c>pdfUrl</c> to open
    /// the attached quotation. The coordinator edits <c>to</c>, <c>cc</c>, <c>subject</c> and <c>body</c> on screen and sends them back in
    /// <c>send</c> or <c>accept</c>.
    /// </remarks>
    [RequirePermission(Permissions.QuotesView)]
    [HttpGet("{quoteId:guid}/email")]
    [ProducesResponseType<QuoteEmailPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuoteEmailPreviewResponse>> PreviewEmail(
        Guid requestId, Guid quoteId, [FromQuery] string action = "send", CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await quoteService.PreviewEmailAsync(requestId, quoteId, action, actorId, role, null, cancellationToken));
    }

    /// <summary>
    /// Accept the request: the quotation is closed as <c>accepted</c> (request becomes <c>unassigned</c>, waiting for an inspector) and its PDF
    /// is emailed, as a reply, to the sender of the original request. Works on a draft (it is sent at the same time,
    /// so every item needs a price) and on an already sent quotation. A failed email (<c>502</c>) changes nothing.
    /// </summary>
    [RequirePermission(Permissions.QuotesWork)]
    [HttpPost("{quoteId:guid}/accept")]
    [ProducesResponseType<QuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<QuoteResponse>> Accept(
        Guid requestId, Guid quoteId, [FromBody] SendQuoteEmailRequest? email, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await quoteService.AcceptAsync(requestId, quoteId, actorId, role, email, cancellationToken));
    }

    /// <summary>
    /// Record the outcome of a sent quotation as <c>accepted</c> or <c>rejected</c> (a reason is required for a
    /// rejection) without sending any email, for example when the client answered by phone.
    /// </summary>
    [RequirePermission(Permissions.QuotesWork)]
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
