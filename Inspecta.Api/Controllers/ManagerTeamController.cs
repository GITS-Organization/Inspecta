using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Operations for a technical manager's own team.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TenantManagement)]
[Route("api/v1/manager/team")]
public sealed class ManagerTeamController(IUserManagementService userManagementService) : ControllerBase
{
    /// <summary>List coordinators and inspectors assigned to the current manager.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AuthUserResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuthUserResponse>>> List(
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken) =>
        Ok(await userManagementService.ListTeamAsync(ResolveManagerId(managerId), cancellationToken));

    /// <summary>Create a coordinator or inspector assigned to the current manager.</summary>
    [HttpPost]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthUserResponse>> Create(
        CreateTeamMemberRequest request,
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken)
    {
        var targetManagerId = ResolveManagerId(managerId);
        var user = await userManagementService.CreateTeamMemberAsync(targetManagerId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { userId = user.Id, managerId = targetManagerId }, user);
    }

    /// <summary>Get a team member assigned to the current manager.</summary>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthUserResponse>> GetById(
        Guid userId,
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken)
    {
        var user = await userManagementService.GetTeamMemberAsync(ResolveManagerId(managerId), userId, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Update a team member's profile without changing their role or manager.</summary>
    [HttpPut("{userId:guid}")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthUserResponse>> Update(
        Guid userId,
        UpdateTeamMemberRequest request,
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken) =>
        Ok(await userManagementService.UpdateTeamMemberAsync(ResolveManagerId(managerId), userId, request, cancellationToken));

    /// <summary>Enable or disable a team member without deleting the account.</summary>
    [HttpPut("{userId:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        Guid userId,
        SetUserStatusRequest request,
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken)
    {
        await userManagementService.SetTeamMemberActiveAsync(
            ResolveManagerId(managerId),
            userId,
            request.IsActive,
            cancellationToken);
        return NoContent();
    }

    /// <summary>Soft-delete a team member assigned to the current manager.</summary>
    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        Guid userId,
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken)
    {
        await userManagementService.DeactivateTeamMemberAsync(ResolveManagerId(managerId), userId, cancellationToken);
        return NoContent();
    }

    private Guid ResolveManagerId(Guid? requestedManagerId)
    {
        var actorId = GetActorId();
        if (User.IsInRole("admin"))
            return requestedManagerId
                ?? throw new AuthException("manager_required", "Admins must provide a managerId.", 400);

        if (requestedManagerId is Guid managerId && managerId != actorId)
            throw new AuthException("forbidden", "Technical managers can only manage their own team.", 403);

        return actorId;
    }

    private Guid GetActorId()
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(subject, out var actorId)
            ? actorId
            : throw new AuthException("unauthorized", "Authenticated user id is invalid.");
    }
}
