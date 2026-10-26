using Inspecta.Application.Contracts.Clients;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Manage client organizations and their contacts.</summary>
[ApiController]
[Authorize(Roles = "admin,coordinator")]
[Route("api/v1/clients")]
public sealed class ClientsController(IClientService clientService) : ControllerBase
{
    /// <summary>List clients, optionally including archived records.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ClientResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClientResponse>>> List([FromQuery] bool includeArchived, CancellationToken cancellationToken) =>
        Ok(await clientService.ListAsync(includeArchived, cancellationToken));

    /// <summary>Get a client by identifier.</summary>
    [HttpGet("{clientId:guid}")]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientResponse>> GetById(Guid clientId, CancellationToken cancellationToken)
    {
        var client = await clientService.GetByIdAsync(clientId, cancellationToken);
        return client is null ? NotFound() : Ok(client);
    }

    /// <summary>Create a client and its contacts.</summary>
    [HttpPost]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ClientResponse>> Create(CreateClientRequest request, CancellationToken cancellationToken)
    {
        var client = await clientService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { clientId = client.Id }, client);
    }

    /// <summary>Archive a client.</summary>
    [HttpDelete("{clientId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(Guid clientId, CancellationToken cancellationToken)
    {
        await clientService.ArchiveAsync(clientId, cancellationToken);
        return NoContent();
    }
}