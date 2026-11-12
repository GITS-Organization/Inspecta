using Inspecta.Application.Authorization;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Mail;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>
/// The email addresses (or whole domains) a client writes from. Mail from one of them is attached to the client and
/// becomes a real request; the same address also moves the client's waiting "maybe" emails into requests.
/// </summary>
[ApiController]
[Route("api/v1/clients/{clientId:guid}/emails")]
public sealed class ClientEmailsController(IClientMailRuleService ruleService) : ControllerBase
{
    /// <summary>List the addresses and domains registered for the client.</summary>
    /// <remarks>
    /// The emails of the client's contacts and its certificates email are recognised as well, even though they are not in this list.
    /// </remarks>
    [RequirePermission(Permissions.ClientsView)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ClientMailRuleResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ClientMailRuleResponse>>> List(Guid clientId, CancellationToken cancellationToken)
    {
        var tenant = Tenant();
        if (!User.IsInRole("admin") && tenant is null) return Unauthorized();
        return Ok(await ruleService.ListForClientAsync(clientId, tenant, cancellationToken));
    }

    /// <summary>Register another email address or domain for the client.</summary>
    /// <remarks>
    /// <c>promotedRequests</c> in the answer is how many "maybe" emails from this sender became real requests of the client.
    /// </remarks>
    [RequirePermission(Permissions.ClientsEdit)]
    [HttpPost]
    [ProducesResponseType<ClientMailRuleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientMailRuleResponse>> Add(
        Guid clientId,
        AddClientEmailRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = Tenant();
        if (!User.IsInRole("admin") && tenant is null) return Unauthorized();
        var created = await ruleService.AddForClientAsync(clientId, tenant, User.GetUserId(), request, cancellationToken);
        return Created($"/api/v1/clients/{clientId}/emails", created);
    }

    /// <summary>Remove an address or domain from the client.</summary>
    [RequirePermission(Permissions.ClientsEdit)]
    [HttpDelete("{ruleId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid clientId, Guid ruleId, CancellationToken cancellationToken)
    {
        var tenant = Tenant();
        if (!User.IsInRole("admin") && tenant is null) return Unauthorized();
        await ruleService.RemoveForClientAsync(clientId, ruleId, tenant, cancellationToken);
        return NoContent();
    }

    private Guid? Tenant() => User.IsInRole("admin") ? null : User.GetTenantId();
}
