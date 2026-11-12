using Inspecta.Application.Authorization;
using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Manage inspection requests and their workflow transitions.</summary>
[ApiController]
[Route("api/v1/requests")]
public sealed class RequestsController(
    IInspectionRequestService requestService,
    IQuoteService quoteService,
    IRequestTimelineService timelineService,
    IRequestTransferService transferService) : ControllerBase
{
    /// <summary>List requests visible to the current user, newest first, one page at a time.</summary>
    /// <remarks>
    /// <c>triage</c> is <c>request</c> (default) for real requests or <c>maybe</c> for emails from unknown senders
    /// that wait for the coordinator. <c>status</c> filters by workflow status (for example <c>quote_draft</c>),
    /// <c>q</c> searches client, location, subject and sender. <c>pageSize</c> defaults to 50 (max 100).
    /// Requests whose quotation is open (<c>quote_draft</c>, <c>quote_sent</c>, <c>counter_offer_sent</c>, <c>client_countered</c>) are not in the default list: they
    /// are worked on in <c>GET /quotations</c>. Ask for them with an explicit <c>status</c>.
    /// Admin may narrow the global view with <c>managerId</c>. <c>from</c> and <c>to</c> (<c>yyyy-MM-dd</c>, either or both, both days included, UTC) keep only the requests received in that period; <c>GET /requests/summary</c> takes the same two dates, so the tab counts follow the filter.
    /// </remarks>
    [RequirePermission(Permissions.RequestsView)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RequestResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RequestResponse>>> List(
        [FromQuery] Guid? managerId,
        [FromQuery] string? triage,
        [FromQuery] string? status,
        [FromQuery] string? q,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = RequestListQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var query = new RequestListQuery(
            managerId,
            string.IsNullOrWhiteSpace(triage) ? null : ParseTriage(triage),
            string.IsNullOrWhiteSpace(status) ? null : DomainEnumParser.ParseJobStatus(status),
            q,
            page,
            pageSize,
            from,
            to);
        return Ok(await requestService.ListVisibleAsync(actorId, role, query, cancellationToken));
    }

    /// <summary>Counts of real requests (in total and per status) and of maybe emails visible to the current user.</summary>
    [RequirePermission(Permissions.RequestsView)]
    [HttpGet("summary")]
    [ProducesResponseType<RequestSummaryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestSummaryResponse>> Summary(
        [FromQuery] Guid? managerId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await requestService.GetSummaryAsync(actorId, role, managerId, from, to, cancellationToken));
    }

    /// <summary>Turn a maybe email into a real request. Owning coordinator only.</summary>
    /// <remarks>
    /// The sender must already belong to a client (an address added with <c>POST /clients/{clientId}/emails</c>, or the email of one of the client's contacts); the request is then attached to that client.
    /// Otherwise the answer is <c>409</c> with code <c>client_required</c>. Adding the sender to the client moves its waiting maybe emails by itself, so this call is rarely needed.
    /// </remarks>
    [RequirePermission(Permissions.RequestsWork)]
    [HttpPost("{requestId:guid}/confirm")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestResponse>> Confirm(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await requestService.ConfirmAsync(requestId, actorId, role, cancellationToken));
    }

    /// <summary>Delete a maybe email now instead of waiting for the automatic cleanup after 7 days. Owning coordinator only.</summary>
    [RequirePermission(Permissions.RequestsWork)]
    [HttpDelete("{requestId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteMaybe(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        await requestService.DeleteMaybeAsync(requestId, actorId, role, cancellationToken);
        return NoContent();
    }

    private static RequestTriage ParseTriage(string value) =>
        Enum.TryParse<RequestTriage>(value, ignoreCase: true, out var triage) && Enum.IsDefined(triage)
            ? triage
            : throw new ArgumentException("triage must be 'request' or 'maybe'.");

    /// <summary>Get a request visible to the current user.</summary>
    [RequirePermission(Permissions.RequestsView)]
    [HttpGet("{requestId:guid}")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestResponse>> GetById(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var request = await requestService.GetVisibleAsync(requestId, actorId, role, cancellationToken);
        return request is null ? NotFound() : Ok(request);
    }

    /// <summary>Everything that happened to a request with its date and time, and how long each stage took.</summary>
    /// <remarks>
    /// For the coordinator, the technical manager and the admin; the inspector gets <c>403</c>, and a request that is not visible to the caller
    /// (another team's, or an email request of another coordinator) is <c>404</c>. Read only, nothing is written.
    /// <c>stages</c> are always the seven stages in order (<c>quoting</c>, <c>client_decision</c>, <c>scheduling</c>, <c>waiting_to_start</c>,
    /// <c>inspection</c>, <c>review</c>, <c>delivery</c>) with <c>from</c>, <c>to</c> and <c>seconds</c> (all <c>null</c> when the request skipped
    /// or has not reached that stage), <c>inProgress</c> for the stage running now (its seconds run until now) and <c>repeats</c> (extra offers,
    /// certificates returned). <c>totalSeconds</c> runs from the request arriving to <c>finishedAt</c> (sent to the client or turned down), or
    /// until now. <c>events</c> are oldest first with <c>at</c>, <c>kind</c> (<c>status</c>, <c>edit</c>, <c>quotation</c>, <c>email</c>,
    /// <c>reply</c>, <c>review</c>, <c>delivery</c>), <c>title</c>, <c>detail</c>, <c>actor</c> (null for the system or the client) and
    /// <c>sincePreviousSeconds</c>. Times are UTC with the offset; durations are calendar time in whole seconds.
    /// </remarks>
    [RequirePermission(Permissions.RequestsTimeline)]
    [HttpGet("{requestId:guid}/timeline")]
    [ProducesResponseType<RequestTimelineResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestTimelineResponse>> Timeline(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var timeline = await timelineService.GetAsync(requestId, actorId, role, cancellationToken);
        return timeline is null ? NotFound() : Ok(timeline);
    }

    /// <summary>Hand a request to another coordinator of the same team.</summary>
    /// <remarks>
    /// <c>coordinatorId</c> comes from <c>GET /team/coordinators</c>; <c>note</c> (optional, up to 500 characters) is added to the history entry
    /// "Transferred from A to B". The status does not change. The coordinator that owns the request can do it until a quotation was sent
    /// (<c>409 not_transferable</c> after that); the technical manager and the admin can do it at any time before the request is finished.
    /// <c>403</c> for another coordinator, <c>404</c> for a request or coordinator of another team, <c>400</c> with <c>not_a_coordinator</c>,
    /// <c>coordinator_inactive</c> or <c>same_coordinator</c>, <c>409 not_transferable</c> for a finished request or an email that is still a
    /// "maybe". An email request then leaves the old coordinator's list and appears in the new one's; the answer is the request.
    /// </remarks>
    [RequirePermission(Permissions.RequestsTransfer)]
    [HttpPost("{requestId:guid}/transfer")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestResponse>> Transfer(Guid requestId, TransferRequest transfer, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await transferService.TransferAsync(requestId, actorId, role, transfer, cancellationToken));
    }

    /// <summary>Create an inspection request.</summary>
    [RequirePermission(Permissions.RequestsWork)]
    [HttpPost]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RequestResponse>> Create(CreateInspectionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var managerId = User.IsInRole("admin") ? request.ManagerId : User.GetTenantId();
        if (!User.IsInRole("admin") && managerId is null) return Unauthorized();
        var created = await requestService.CreateAsync(request, actorId, managerId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { requestId = created.Id }, created);
    }

    /// <summary>Give a request of a client with a contract to an inspector straight away, with no quotation.</summary>
    /// <remarks>
    /// Offered when the request has <c>canAssignWithoutQuotation: true</c>: a new request (<c>request_received</c>) whose client has a contract
    /// (<c>client.hasContract</c>) and whose client and location are filled in. The request becomes <c>assigned</c> with <c>kind: contract</c>
    /// and a placeholder accepted quotation of price 0 (number <c>CT-...</c>), so it shows in the quotation queue like any assigned job.
    /// <c>409 no_contract</c> for a client without a contract, <c>409 not_assignable</c> when the request is not in that stage.
    /// </remarks>
    [RequirePermission(Permissions.RequestsAssign)]
    [HttpPost("{requestId:guid}/assign-without-quotation")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestResponse>> AssignWithoutQuotation(Guid requestId, AssignWithoutQuotationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await requestService.AssignWithoutQuotationAsync(requestId, actorId, role, request, cancellationToken));
    }

    /// <summary>Create a man day: a day of an inspector's time that a client asked for.</summary>
    /// <remarks>
    /// For a client that phones or emails asking for an inspector on a day, with no list of machines. The job has no quotation: it is
    /// given to the inspector straight away (<c>assigned</c>, <c>kind: man_day</c>) and the inspector adds the machines found on site
    /// (<c>POST /quotation-queue/{requestId}/machines</c>) and certifies them. Coordinator, technical manager and Admin.
    /// </remarks>
    [RequirePermission(Permissions.RequestsAssign)]
    [HttpPost("man-day")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestResponse>> CreateManDay(CreateManDayRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var created = await requestService.CreateManDayAsync(request, actorId, role, User.GetTenantId(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { requestId = created.Id }, created);
    }

    /// <summary>The client's machines that the request's email seems to ask about.</summary>
    /// <remarks>
    /// Only machines of the request's client are considered, and those already on the request are left out. Each one has
    /// <c>quantity</c>, <c>confidence</c> (0 to 1), <c>level</c> (<c>high</c>, <c>medium</c> or <c>ambiguous</c>), the <c>reason</c> it was
    /// found (<c>serial</c>, <c>name</c>, <c>model</c>) and the <c>evidence</c> line of the email. Show them as suggestions the coordinator
    /// accepts by adding the machine to the request (<c>equipmentId</c>). A request without a client has none.
    /// </remarks>
    [RequirePermission(Permissions.RequestsView)]
    [HttpGet("{requestId:guid}/suggestions")]
    [ProducesResponseType<IReadOnlyList<MachineSuggestionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MachineSuggestionResponse>>> Suggestions(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await requestService.SuggestMachinesAsync(requestId, actorId, role, cancellationToken));
    }

    /// <summary>Read the email again and put the machines it names on the request. Owning coordinator only.</summary>
    /// <remarks>
    /// Does the same that happens by itself when an email arrives from a known client: when the request has a client and no
    /// items yet, the confident matches (<c>level: high</c>) are added as items linked to the client's machines. A request that
    /// already has items is returned unchanged.
    /// </remarks>
    [RequirePermission(Permissions.RequestsWork)]
    [HttpPost("{requestId:guid}/autofill")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestResponse>> Autofill(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await requestService.AutofillAsync(requestId, actorId, role, cancellationToken));
    }

    /// <summary>Get the original email an email-sourced request was created from.</summary>
    /// <remarks><c>htmlBody</c> is untrusted sender content: sanitize it or render <c>textBody</c> instead.</remarks>
    [RequirePermission(Permissions.RequestsView)]
    [HttpGet("{requestId:guid}/email")]
    [ProducesResponseType<RequestEmailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestEmailResponse>> GetEmail(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var email = await requestService.GetEmailAsync(requestId, actorId, role, cancellationToken);
        return email is null ? NotFound() : Ok(email);
    }

    /// <summary>
    /// Save the request screen in one step: client, location, preferred date, access notes and, optionally, the whole
    /// equipment list (<c>items</c>: items with an <c>id</c> of this request are updated, items without one (or with an id the
    /// request does not know) are added, new machines are registered in the catalog automatically, items left out are
    /// removed; omit <c>items</c> to keep them). Everything is saved together or nothing is. As soon as the request is complete
    /// (client name, location and an item) the quotation is opened by this same save; send <c>prepareQuotation: false</c> to only save,
    /// or <c>true</c> to refuse an incomplete request with <c>409</c>. Details of the opening: 
    /// the quotation in the same save (items copied with empty prices, request moves to <c>quote_draft</c>, the response carries
    /// <c>quotation</c>; a complete request is required, otherwise <c>409 request_incomplete</c>). Send the <c>updatedAt</c> you
    /// read as <c>expectedUpdatedAt</c> to get <c>409</c> instead of overwriting newer changes. Owning coordinator only,
    /// before a quote is sent.
    /// </summary>
    [RequirePermission(Permissions.RequestsWork)]
    [HttpPut("{requestId:guid}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestResponse>> UpdateDetails(Guid requestId, UpdateRequestDetailsRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(request.PrepareQuotation is not false
            ? await quoteService.SaveRequestAndPrepareQuotationAsync(requestId, actorId, role, request, cancellationToken)
            : await requestService.UpdateDetailsAsync(requestId, actorId, role, request, cancellationToken));
    }

    /// <summary>Add an equipment item to a request. Owning coordinator only, before a quote is sent.</summary>
    [RequirePermission(Permissions.RequestsWork)]
    [HttpPost("{requestId:guid}/items")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestResponse>> AddItem(Guid requestId, CreateRequestItem item, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var updated = await requestService.AddItemAsync(requestId, actorId, role, item, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { requestId }, updated);
    }

    /// <summary>Replace an equipment item of a request. Owning coordinator only, before a quote is sent.</summary>
    [RequirePermission(Permissions.RequestsWork)]
    [HttpPut("{requestId:guid}/items/{itemId:guid}")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestResponse>> UpdateItem(Guid requestId, Guid itemId, CreateRequestItem item, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await requestService.UpdateItemAsync(requestId, itemId, actorId, role, item, cancellationToken));
    }

    /// <summary>Remove an equipment item from a request. Owning coordinator only, before a quote is sent.</summary>
    [RequirePermission(Permissions.RequestsWork)]
    [HttpDelete("{requestId:guid}/items/{itemId:guid}")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestResponse>> RemoveItem(Guid requestId, Guid itemId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await requestService.RemoveItemAsync(requestId, itemId, actorId, role, cancellationToken));
    }

    /// <summary>
    /// Turn the request down with a reason, from any stage before acceptance (new request, quotation being prepared, or
    /// quotation sent). The open quotation, if any, is closed as rejected. No email is sent. Owning coordinator only.
    /// </summary>
    [RequirePermission(Permissions.RequestsWork)]
    [HttpPost("{requestId:guid}/reject")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestResponse>> Reject(Guid requestId, RejectRequestRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await quoteService.RejectRequestAsync(requestId, actorId, role, request, cancellationToken));
    }

    /// <summary>
    /// Move a request through the steps after acceptance (inspector work, certificates). Quotation steps and the inspector
    /// assignment are refused with <c>409</c>: use the quotation endpoints, <c>reject</c> and <c>assignment</c> for those.
    /// </summary>
    [RequirePermission(Permissions.RequestsTransition)]
    [HttpPost("{requestId:guid}/transitions")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestResponse>> Transition(Guid requestId, TransitionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await requestService.TransitionAsync(requestId, actorId, role, request, cancellationToken));
    }

    /// <summary>Assign an accepted request to an inspector.</summary>
    [RequirePermission(Permissions.RequestsAssign)]
    [HttpPost("{requestId:guid}/assignment")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestResponse>> Assign(Guid requestId, AssignInspectorRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var updated = await requestService.AssignAsync(requestId, actorId, role, request.InspectorId, request.ScheduledAt, request.Note, cancellationToken);
        return Ok(updated);
    }

    private bool TryGetActor(out Guid actorId, out string role)
    {
        role = User.FindFirst("role")?.Value ?? string.Empty;
        return Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
    }
}

/// <summary>Payload for scheduling an accepted request with an inspector.</summary>
public sealed record AssignInspectorRequest(Guid InspectorId, DateTimeOffset ScheduledAt, string? Note);
