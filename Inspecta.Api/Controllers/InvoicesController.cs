using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Authorization;
using Inspecta.Application.Contracts;
using Inspecta.Application.Contracts.Invoices;
using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Pdf;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>
/// Invoices: what a client owes for an accepted quotation, the money received so far and where the invoice is.
/// An invoice is made automatically when a quotation with a price is accepted. Money is in piastres.
/// </summary>
/// <remarks>
/// <c>stage</c> is <c>open</c> (the coordinator who owns the request records its payments), <c>with_finance</c> (handed over: the
/// financial manager records them and closes it) or <c>closed</c>. The platform owner can always act. The general manager and the
/// financial manager see every invoice but never the request behind it. A mistake in a payment is fixed by reversing it: the payment
/// and its reversal both stay in the history.
/// </remarks>
[ApiController]
[Route("api/v1/invoices")]
public sealed class InvoicesController(IInvoiceService invoiceService) : ControllerBase
{
    /// <summary>
    /// List invoices, newest first. A coordinator sees the invoices of the requests they may see; the platform owner can narrow to one
    /// company with <c>managerId</c>. <c>stage</c>: <c>open</c>, <c>with_finance</c> or <c>closed</c>. <c>paymentState</c>:
    /// <c>unpaid</c>, <c>partial</c> or <c>paid</c>. <c>q</c> searches client, invoice number and quotation reference. <c>from</c> and
    /// <c>to</c> (<c>yyyy-MM-dd</c>, Cairo days, both included) keep the invoices made in that period. <c>pageSize</c> defaults to 50 (max 100).
    /// </summary>
    [RequirePermission(Permissions.InvoicesView)]
    [HttpGet]
    [ProducesResponseType<PagedList<InvoiceSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedList<InvoiceSummary>>> List(
        [FromQuery] string? stage,
        [FromQuery] string? paymentState,
        [FromQuery] string? q,
        [FromQuery] Guid? requestId,
        [FromQuery] Guid? managerId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? reviewerId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await invoiceService.ListAsync(
            actorId, role, new InvoiceListQuery(stage, paymentState, q, requestId, managerId, from, to, page, pageSize, reviewerId), cancellationToken));
    }

    /// <summary>
    /// The company's revenue: the payments recorded from <c>from</c> to <c>to</c> (<c>yyyy-MM-dd</c>, Cairo days, both included; default the
    /// current month), by the day each was received, with the total, a figure per month and one per client (biggest first). VAT included. A
    /// reversal counts as negative money on the day it was recorded, so a past month never changes afterwards. The platform owner can narrow to one
    /// company with <c>managerId</c>.
    /// </summary>
    [RequirePermission(Permissions.RevenueView)]
    [HttpGet("revenue")]
    [ProducesResponseType<RevenueResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RevenueResponse>> Revenue(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? managerId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out _, out var role)) return Unauthorized();
        return Ok(await invoiceService.RevenueAsync(from, to, managerId, role, cancellationToken));
    }

    /// <summary>
    /// One invoice with its frozen numbers and price lines, its payments, its history (oldest first) and <c>actions</c>: what the caller can
    /// do with it now. It holds no detail of the inspection work.
    /// </summary>
    [RequirePermission(Permissions.InvoicesView)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<InvoiceDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await invoiceService.GetAsync(id, actorId, role, cancellationToken));
    }

    /// <summary>
    /// Make the invoice of a job whose quotation was accepted before invoices existed (new acceptances make theirs by themselves).
    /// Answers the invoice that already exists, if there is one. <c>409 no_invoice_needed</c> for a quotation without a price,
    /// <c>409 no_accepted_quote</c> when the request has no accepted quotation.
    /// </summary>
    [RequirePermission(Permissions.InvoicesCreate)]
    [HttpPost("~/api/v1/requests/{requestId:guid}/invoice")]
    [ProducesResponseType<InvoiceDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceDetail>> CreateForRequest(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await invoiceService.CreateForRequestAsync(requestId, actorId, role, cancellationToken));
    }

    /// <summary>
    /// Record money received: <c>amountPiastres</c> (more than zero, not more than the balance), <c>paidOn</c> (from the day the quotation was
    /// accepted up to today, Cairo) and an optional <c>label</c> ("Advance", "After the visit"). The coordinator who owns the request records
    /// while the invoice is open; the financial manager once it is with finance. Errors: <c>overpayment</c>, <c>payment_date_invalid</c>,
    /// <c>payment_amount_invalid</c>, <c>invoice_closed</c>, <c>wrong_stage</c>.
    /// </summary>
    [RequirePermission(Permissions.InvoicesRecordPayment)]
    [HttpPost("{id:guid}/payments")]
    [ProducesResponseType<InvoiceDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceDetail>> RecordPayment(Guid id, [FromBody] RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await invoiceService.RecordPaymentAsync(id, request, actorId, role, cancellationToken));
    }

    /// <summary>
    /// Undo a payment that was recorded by mistake. Nothing is deleted: a reversing row (a negative amount dated today) is added and the
    /// balance comes back. A reason is required. Errors: <c>already_reversed</c>, <c>not_a_payment</c>, <c>reason_required</c>.
    /// </summary>
    [RequirePermission(Permissions.InvoicesRecordPayment)]
    [HttpPost("{id:guid}/payments/{paymentId:guid}/reverse")]
    [ProducesResponseType<InvoiceDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceDetail>> ReversePayment(
        Guid id, Guid paymentId, [FromBody] ReversePaymentRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await invoiceService.ReversePaymentAsync(id, paymentId, request, actorId, role, cancellationToken));
    }

    /// <summary>The invoice as a PDF (its frozen numbers and price lines, what is paid and what is left as of today), for anyone who may view invoices.</summary>
    [RequirePermission(Permissions.InvoicesView)]
    [HttpGet("{id:guid}/pdf")]
    [Produces(PdfFile.ContentType)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var file = await invoiceService.GetPdfAsync(id, actorId, role, cancellationToken);
        return File(file.Content, PdfFile.ContentType, file.FileName);
    }

    /// <summary>
    /// The email that would carry the invoice to the client, ready to be edited. It goes to the sender of the original request email (or the client's
    /// certificates email, or the address in <c>sentTo</c>). The answer has <c>canSend</c> and <c>blockers</c> (why Send would fail: the certificates are not
    /// delivered yet, no mailbox, no address) and <c>pdfUrl</c> to open the attachment. Sends nothing.
    /// </summary>
    [RequirePermission(Permissions.InvoicesEmailClient)]
    [HttpGet("{id:guid}/email")]
    [ProducesResponseType<QuoteEmailPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuoteEmailPreviewResponse>> PreviewEmail(
        Guid id, [FromQuery] string? sentTo, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var draft = string.IsNullOrWhiteSpace(sentTo) ? null : new SendQuoteEmailRequest { SentTo = sentTo };
        return Ok(await invoiceService.PreviewEmailAsync(id, draft, actorId, role, cancellationToken));
    }

    /// <summary>
    /// Email the invoice PDF to the client from the coordinator's mailbox. Only after the certificates were delivered (<c>409 not_delivered</c>) and
    /// while the invoice is not closed; it can be sent again. Everything in the body is optional and keeps what the preview suggested. Only after the mail
    /// server accepted the message is the sending recorded in the invoice history; a failure (<c>502</c>) changes nothing and can be retried. It does not
    /// hand the invoice over: that is <c>POST .../hand-over</c>.
    /// </summary>
    [RequirePermission(Permissions.InvoicesEmailClient)]
    [HttpPost("{id:guid}/email")]
    [ProducesResponseType<InvoiceDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<InvoiceDetail>> SendEmail(Guid id, [FromBody] SendQuoteEmailRequest? request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await invoiceService.SendEmailAsync(id, request, actorId, role, cancellationToken));
    }

    /// <summary>
    /// Hand the invoice to the financial manager. Only once the certificates were delivered to the client (<c>409 not_delivered</c>), and only
    /// once (<c>409 wrong_stage</c>). Emailing the client first is optional.
    /// </summary>
    [RequirePermission(Permissions.InvoicesHandOver)]
    [HttpPost("{id:guid}/hand-over")]
    [ProducesResponseType<InvoiceDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceDetail>> HandOver(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await invoiceService.HandOverAsync(id, actorId, role, cancellationToken));
    }

    /// <summary>
    /// Close the invoice. Without a body (or without a <c>reason</c>) it closes only when the balance is zero (<c>409 balance_not_zero</c>)
    /// and the invoice is with finance. With a <c>reason</c> it closes whatever is still owed, for example a job that was never delivered.
    /// After it is closed nothing changes (the platform owner can still reverse a payment).
    /// </summary>
    [RequirePermission(Permissions.InvoicesClose)]
    [HttpPost("{id:guid}/close")]
    [ProducesResponseType<InvoiceDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceDetail>> Close(Guid id, [FromBody] CloseInvoiceRequest? request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await invoiceService.CloseAsync(id, request, actorId, role, cancellationToken));
    }

    private bool TryGetActor(out Guid actorId, out string role)
    {
        role = User.FindFirst("role")?.Value ?? string.Empty;
        return Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
    }
}
