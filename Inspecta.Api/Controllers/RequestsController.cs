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
[Authorize(Policy = AuthorizationPolicies.TeamAccess)]
[Route("api/v1/requests")]
public sealed class RequestsController(IInspectionRequestService requestService) : ControllerBase
{
    /// <summary>List requests visible to the current user, newest first, one page at a time.</summary>
    /// <remarks>
    /// <c>triage</c> is <c>request</c> (default) for real requests or <c>maybe</c> for emails from unknown senders
    /// that wait for the coordinator. <c>status</c> filters by workflow status (for example <c>quote_draft</c>),
    /// <c>q</c> searches client, location, subject and sender. <c>pageSize</c> defaults to 50 (max 100).
    /// Admin may narrow the global view with <c>managerId</c>. Use <c>GET /requests/summary</c> for tab counts.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RequestResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RequestResponse>>> List(
        [FromQuery] Guid? managerId,
        [FromQuery] string? triage,
        [FromQuery] string? status,
        [FromQuery] string? q,
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
            pageSize);
        return Ok(await requestService.ListVisibleAsync(actorId, role, query, cancellationToken));
    }

    /// <summary>Counts of real requests (in total and per status) and of maybe emails visible to the current user.</summary>
    [HttpGet("summary")]
    [ProducesResponseType<RequestSummaryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestSummaryResponse>> Summary([FromQuery] Guid? managerId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await requestService.GetSummaryAsync(actorId, role, managerId, cancellationToken));
    }

    /// <summary>Turn a maybe email into a real request. Owning coordinator only.</summary>
    [HttpPost("{requestId:guid}/confirm")]
    [Authorize(Policy = AuthorizationPolicies.TeamOperations)]
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
    [HttpDelete("{requestId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TeamOperations)]
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
    [HttpGet("{requestId:guid}")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestResponse>> GetById(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var request = await requestService.GetVisibleAsync(requestId, actorId, role, cancellationToken);
        return request is null ? NotFound() : Ok(request);
    }

    /// <summary>Create an inspection request.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.TeamOperations)]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RequestResponse>> Create(CreateInspectionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out _)) return Unauthorized();
        var managerId = User.IsInRole("admin") ? request.ManagerId : User.GetTenantId();
        if (!User.IsInRole("admin") && managerId is null) return Unauthorized();
        var created = await requestService.CreateAsync(request, actorId, managerId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { requestId = created.Id }, created);
    }

    /// <summary>Get the original email an email-sourced request was created from.</summary>
    /// <remarks><c>htmlBody</c> is untrusted sender content: sanitize it or render <c>textBody</c> instead.</remarks>
    [HttpGet("{requestId:guid}/email")]
    [ProducesResponseType<RequestEmailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestEmailResponse>> GetEmail(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var email = await requestService.GetEmailAsync(requestId, actorId, role, cancellationToken);
        return email is null ? NotFound() : Ok(email);
    }

    /// <summary>Complete client, location, preferred date and access notes. Owning coordinator only, before a quote is sent.</summary>
    [HttpPut("{requestId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TeamOperations)]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestResponse>> UpdateDetails(Guid requestId, UpdateRequestDetailsRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await requestService.UpdateDetailsAsync(requestId, actorId, role, request, cancellationToken));
    }

    /// <summary>Add an equipment item to a request. Owning coordinator only, before a quote is sent.</summary>
    [HttpPost("{requestId:guid}/items")]
    [Authorize(Policy = AuthorizationPolicies.TeamOperations)]
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
    [HttpPut("{requestId:guid}/items/{itemId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TeamOperations)]
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
    [HttpDelete("{requestId:guid}/items/{itemId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TeamOperations)]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestResponse>> RemoveItem(Guid requestId, Guid itemId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await requestService.RemoveItemAsync(requestId, itemId, actorId, role, cancellationToken));
    }

    /// <summary>Advance a request to the requested workflow status.</summary>
    [HttpPost("{requestId:guid}/transitions")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestResponse>> Transition(Guid requestId, TransitionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await requestService.TransitionAsync(requestId, actorId, role, request, cancellationToken));
    }

    /// <summary>Assign an accepted request to an inspector.</summary>
    [HttpPost("{requestId:guid}/assignment")]
    [Authorize(Policy = AuthorizationPolicies.TeamOperations)]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestResponse>> Assign(Guid requestId, AssignInspectorRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out _)) return Unauthorized();
        var updated = await requestService.AssignAsync(requestId, actorId, request.InspectorId, request.ScheduledAt, request.Note, cancellationToken);
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
