using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Clients;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Admin only: add, change and remove clients of any team (used for testing).</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v1/admin/clients")]
public sealed class AdminClientsController(IClientService clientService) : ControllerBase
{
    /// <summary>List the clients of every team.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ClientResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClientResponse>>> List([FromQuery] bool includeArchived, CancellationToken cancellationToken) =>
        Ok(await clientService.ListAsync(includeArchived, cancellationToken));

    /// <summary>Open one client with its contacts.</summary>
    [HttpGet("{clientId:guid}")]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientResponse>> Get(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await clientService.GetByIdAsync(clientId, cancellationToken);
        return client is null ? NotFound() : Ok(client);
    }

    /// <summary>Add a client to a team.</summary>
    /// <remarks><c>managerId</c> (the technical manager whose team the client belongs to) is required.</remarks>
    [HttpPost]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClientResponse>> Create(CreateClientRequest request, CancellationToken cancellationToken)
    {
        if (request.ManagerId is null)
            return BadRequest(new ProblemDetails { Title = "managerId is required: the technical manager whose team the client belongs to." });
        var client = await clientService.CreateAsync(request, request.ManagerId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { clientId = client.Id }, client);
    }

    /// <summary>Change a client's details and contacts.</summary>
    [HttpPut("{clientId:guid}")]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientResponse>> Update(Guid clientId, UpdateClientRequest request, CancellationToken cancellationToken) =>
        Ok(await clientService.UpdateAsync(clientId, request, cancellationToken));

    /// <summary>Remove a client: archive it (default) or delete it for good.</summary>
    /// <remarks>
    /// <c>permanent=false</c> (default) archives the client: it leaves the lists and can no longer be edited.
    /// <c>permanent=true</c> deletes the client and its contacts for good, and is refused with <c>409 client_in_use</c>
    /// while requests or registered items still use it.
    /// </remarks>
    [HttpDelete("{clientId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid clientId, [FromQuery] bool permanent, CancellationToken cancellationToken)
    {
        if (permanent) await clientService.DeleteAsync(clientId, cancellationToken);
        else await clientService.ArchiveAsync(clientId, cancellationToken);
        return NoContent();
    }
}
