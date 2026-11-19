using Inspecta.Api.Authorization;
using Inspecta.Application.Authorization;
using Inspecta.Application.Contracts.Renewals;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>
/// Renewals: the machines whose next inspection is coming up, grouped by client, so the coordinator can ask the client first and turn the answer into a request.
/// The platform owner names the company with <c>managerId</c>; everyone else always gets their own company.
/// </summary>
[ApiController]
[Route("api/v1/renewals")]
public sealed class RenewalsController(IRenewalsService renewals) : ControllerBase
{
    /// <summary>
    /// The clients that have machines due. <c>window</c>: <c>overdue</c>, <c>30</c> (default), <c>60</c> or <c>90</c> (days; the counts and the windows include the
    /// overdue machines). Each client lists every active machine that is overdue or due within 90 days, <c>inWindow</c> says which ones to tick, and
    /// <c>onOpenRequest</c> marks machines that are already on a request. Retired and archived machines never appear. <c>recipients</c> are the addresses a
    /// reminder may be sent to. <c>q</c> searches the client name; <c>pageSize</c> default 50, max 100.
    /// </summary>
    [RequirePermission(Permissions.RenewalsWork)]
    [HttpGet]
    [ProducesResponseType<RenewalsListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RenewalsListResponse>> List(
        [FromQuery] string? window, [FromQuery] string? q, [FromQuery] Guid? managerId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(await renewals.ListAsync(ResolveCompany(managerId), window, q, page, pageSize, cancellationToken));

    /// <summary>
    /// The email the client would receive for these machines, with the reasons Send would not work (<c>blockers</c>). Sends nothing and saves nothing.
    /// Same body as Send, <c>sendKey</c> not needed. 400 <c>recipient_not_of_client</c>, <c>machine_not_eligible</c>, <c>machines_required</c>, <c>message_required</c>.
    /// </summary>
    [RequirePermission(Permissions.RenewalsWork)]
    [HttpPost("reminders/preview")]
    [ProducesResponseType<RenewalReminderPreview>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RenewalReminderPreview>> Preview(
        SendRenewalReminderRequest request, [FromQuery] Guid? managerId, CancellationToken cancellationToken) =>
        Ok(await renewals.PreviewReminderAsync(ResolveCompany(managerId), User.GetUserId(), request, cancellationToken));

    /// <summary>
    /// Sends the reminder from the caller's own mailbox: a text of the coordinator's with a table of the machines, to one of the client's saved addresses, with no
    /// attachment. Nothing is ever sent automatically. <c>sendKey</c> (a new UUID per opened drawer, reused for a retry) makes a double click send once: the
    /// same key answers 200 with the same reminder and sends nothing. 201 when it was sent now.
    /// Errors: 400 <c>send_key_required</c>, <c>machines_required</c>, <c>message_required</c>, <c>message_too_long</c>, <c>recipient_not_of_client</c>,
    /// <c>machine_not_eligible</c>; 404 client not found; 409 <c>mailbox_not_configured</c>, <c>mailbox_reconnect_required</c>, <c>email_disabled</c>,
    /// <c>reminder_in_progress</c>; 502 <c>email_delivery_failed</c> (kept as failed; retry with the same key).
    /// </summary>
    [RequirePermission(Permissions.RenewalsWork)]
    [HttpPost("reminders")]
    [ProducesResponseType<RenewalReminderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<RenewalReminderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<RenewalReminderResponse>> Send(
        SendRenewalReminderRequest request, [FromQuery] Guid? managerId, CancellationToken cancellationToken)
    {
        var result = await renewals.SendReminderAsync(ResolveCompany(managerId), User.GetUserId(), request, cancellationToken);
        return result.Created
            ? CreatedAtAction(nameof(Get), new { reminderId = result.Reminder.Id, managerId }, result.Reminder)
            : Ok(result.Reminder);
    }

    /// <summary>
    /// "Create renewal request": a normal request for the client with the ticked machines (and an optional preferred date), made after the client answered
    /// (<c>reminderId</c>) or without any reminder. It then continues like any request (quotation, inspection, certificate) and answers the same body as
    /// <c>POST /requests</c>. A reminder becomes one request only: 409 <c>renewal_request_exists</c>. 400 <c>machines_required</c>, <c>machine_not_eligible</c>
    /// (another client's, out of service or archived), <c>reminder_of_other_client</c>; 409 <c>reminder_not_sent</c>, <c>reminder_closed</c>; 404 client or reminder not found.
    /// </summary>
    [RequirePermission(Permissions.RenewalsWork)]
    [HttpPost("requests")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestResponse>> CreateRequest(
        CreateRenewalRequest request, [FromQuery] Guid? managerId, CancellationToken cancellationToken)
    {
        var created = await renewals.CreateRequestAsync(ResolveCompany(managerId), User.GetUserId(), request, cancellationToken);
        return CreatedAtAction("GetById", "Requests", new { requestId = created.Id }, created);
    }

    /// <summary>The client said no, or it was handled another way: the reminder ends without a request (state <c>closed</c>). Closing again changes nothing. 409 <c>renewal_request_exists</c> once it became a request.</summary>
    [RequirePermission(Permissions.RenewalsWork)]
    [HttpPost("reminders/{reminderId:guid}/close")]
    [ProducesResponseType<RenewalReminderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RenewalReminderResponse>> Close(Guid reminderId, [FromQuery] Guid? managerId, CancellationToken cancellationToken) =>
        Ok(await renewals.CloseReminderAsync(ResolveCompany(managerId), User.GetUserId(), reminderId, cancellationToken));

    /// <summary>One reminder with the client's answers (as text only) and, once it became a request, which one.</summary>
    [RequirePermission(Permissions.RenewalsWork)]
    [HttpGet("reminders/{reminderId:guid}")]
    [ProducesResponseType<RenewalReminderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RenewalReminderResponse>> Get(Guid reminderId, [FromQuery] Guid? managerId, CancellationToken cancellationToken) =>
        Ok(await renewals.GetReminderAsync(ResolveCompany(managerId), reminderId, cancellationToken));

    /// <summary>The reminders, newest first, of one client (<c>clientId</c>) or all. <c>pageSize</c> default 50, max 100.</summary>
    [RequirePermission(Permissions.RenewalsWork)]
    [HttpGet("reminders")]
    [ProducesResponseType<RenewalReminderPage>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RenewalReminderPage>> ListReminders(
        [FromQuery] Guid? clientId, [FromQuery] Guid? managerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(await renewals.ListRemindersAsync(ResolveCompany(managerId), clientId, page, pageSize, cancellationToken));

    private Guid ResolveCompany(Guid? requested)
    {
        if (User.IsInRole("admin"))
            return requested ?? throw new AuthException("manager_required", "The platform owner must provide a managerId.", 400);
        return User.GetTenantId() ?? throw new AuthException("forbidden", "The account does not belong to a company.", 403);
    }
}
