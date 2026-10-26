using System.IdentityModel.Tokens.Jwt;
using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

[ApiController]
[Authorize(Roles = "technical_manager")]
[Route("api/v1/manager/team")]
public sealed class ManagerTeamController(IUserManagementService userManagementService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AuthUserResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuthUserResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await userManagementService.ListTeamAsync(GetActorId(), cancellationToken));

    [HttpPost]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthUserResponse>> Create(
        CreateTeamMemberRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManagementService.CreateTeamMemberAsync(GetActorId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { userId = user.Id }, user);
    }

    [HttpGet("{userId:guid}")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthUserResponse>> GetById(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManagementService.GetTeamMemberAsync(GetActorId(), userId, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPut("{userId:guid}")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthUserResponse>> Update(
        Guid userId,
        UpdateTeamMemberRequest request,
        CancellationToken cancellationToken) =>
        Ok(await userManagementService.UpdateTeamMemberAsync(GetActorId(), userId, request, cancellationToken));

    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid userId, CancellationToken cancellationToken)
    {
        await userManagementService.DeactivateTeamMemberAsync(GetActorId(), userId, cancellationToken);
        return NoContent();
    }

    private Guid GetActorId()
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(subject, out var actorId)
            ? actorId
            : throw new AuthException("unauthorized", "Authenticated user id is invalid.");
    }
}
