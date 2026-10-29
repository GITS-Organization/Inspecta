using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>The inspectors a job can be given to.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamOperations)]
[Route("api/v1/inspectors")]
public sealed class InspectorsController(IInspectionRequestService requestService) : ControllerBase
{
    /// <summary>
    /// List the active inspectors of the team, with whether they are on leave and how many unfinished jobs they already have.
    /// </summary>
    /// <remarks>
    /// A coordinator and a manager get their own team. Admin gets every team, or one with <c>managerId</c>. Use an
    /// <c>id</c> from here as <c>inspectorId</c> in <c>POST /requests/{requestId}/assignment</c>, and to list what an inspector
    /// was given with <c>GET /quotations?inspectorId=...</c>.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InspectorResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InspectorResponse>>> List(
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken)
    {
        var role = User.FindFirst("role")?.Value ?? string.Empty;
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var actorId)) return Unauthorized();
        return Ok(await requestService.ListInspectorsAsync(actorId, role, managerId, cancellationToken));
    }
}
