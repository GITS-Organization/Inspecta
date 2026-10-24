using System.IdentityModel.Tokens.Jwt;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/requests")]
public sealed class RequestsController(IInspectionRequestService requestService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RequestResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RequestResponse>>> List(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await requestService.ListVisibleAsync(actorId, role, cancellationToken));
    }

    [HttpGet("{requestId:guid}")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestResponse>> GetById(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var request = await requestService.GetVisibleAsync(requestId, actorId, role, cancellationToken);
        return request is null ? NotFound() : Ok(request);
    }

    [HttpPost]
    [Authorize(Roles = "admin,coordinator")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RequestResponse>> Create(CreateInspectionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out _)) return Unauthorized();
        var created = await requestService.CreateAsync(request, actorId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { requestId = created.Id }, created);
    }

    [HttpPost("{requestId:guid}/transitions")]
    [ProducesResponseType<RequestResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestResponse>> Transition(Guid requestId, TransitionRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await requestService.TransitionAsync(requestId, actorId, role, request, cancellationToken));
    }

    [HttpPost("{requestId:guid}/assignment")]
    [Authorize(Roles = "admin,coordinator")]
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