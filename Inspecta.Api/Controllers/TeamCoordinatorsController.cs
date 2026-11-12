using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>The coordinators of a team, for picking the one a request is handed to.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamOperations)]
[Route("api/v1/team/coordinators")]
public sealed class TeamCoordinatorsController(IRequestTransferService transferService) : ControllerBase
{
    /// <summary>The active coordinators of the caller's team (the caller left out), by name.</summary>
    /// <remarks>
    /// For a coordinator, the technical manager or the admin. Only <c>id</c>, <c>fullName</c> and <c>email</c>: no mailbox details. The admin
    /// names the team with <c>managerId</c>; without it the list is empty. Use the <c>id</c> in <c>POST /requests/{requestId}/transfer</c>.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TeamCoordinatorResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamCoordinatorResponse>>> List([FromQuery] Guid? managerId, CancellationToken cancellationToken)
    {
        var role = User.FindFirst("role")?.Value ?? string.Empty;
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var actorId)) return Unauthorized();
        return Ok(await transferService.ListCoordinatorsAsync(actorId, role, managerId, cancellationToken));
    }
}
