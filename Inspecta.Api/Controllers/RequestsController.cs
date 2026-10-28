using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Manage inspection requests and their workflow transitions.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamAccess)]
[Route("api/v1/requests")]
public sealed class RequestsController(IInspectionRequestService requestService) : ControllerBase
{
    /// <summary>List requests visible to the current user. Admin may narrow the global view with <c>managerId</c>.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RequestResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RequestResponse>>> List([FromQuery] Guid? managerId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await requestService.ListVisibleAsync(actorId, role, managerId, cancellationToken));
    }

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
