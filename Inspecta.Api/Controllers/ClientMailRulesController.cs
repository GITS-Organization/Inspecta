using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Mail;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>
/// Client sender addresses and domains of a team. Mail from a listed sender becomes a request immediately;
/// mail from anyone else waits as a "maybe" until the coordinator confirms or deletes it.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamOperations)]
[Route("api/v1/client-mail-rules")]
public sealed class ClientMailRulesController(IClientMailRuleService ruleService) : ControllerBase
{
    /// <summary>List the senders of the current team. Admin sees every team, or one with <c>managerId</c>.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ClientMailRuleResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClientMailRuleResponse>>> List(
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken)
    {
        var scope = User.IsInRole("admin") ? managerId : User.GetTenantId();
        if (!User.IsInRole("admin") && scope is null) return Unauthorized();
        return Ok(await ruleService.ListAsync(scope, cancellationToken));
    }

    /// <summary>Add a client sender (address or domain) to the team. Admin must pass <c>managerId</c>.</summary>
    [HttpPost]
    [ProducesResponseType<ClientMailRuleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientMailRuleResponse>> Create(
        CreateClientMailRuleRequest request,
        CancellationToken cancellationToken)
    {
        var managerId = User.IsInRole("admin") ? request.ManagerId : User.GetTenantId();
        if (managerId is null)
            return User.IsInRole("admin")
                ? BadRequest(new ProblemDetails { Title = "Invalid request", Detail = "managerId is required for Admin." })
                : Unauthorized();

        var created = await ruleService.CreateAsync(managerId.Value, User.GetUserId(), request, cancellationToken);
        return Created($"/api/v1/client-mail-rules/{created.Id}", created);
    }

    /// <summary>Remove a client sender from the team.</summary>
    [HttpDelete("{ruleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid ruleId, CancellationToken cancellationToken)
    {
        await ruleService.DeleteAsync(ruleId, cancellationToken);
        return NoContent();
    }
}
