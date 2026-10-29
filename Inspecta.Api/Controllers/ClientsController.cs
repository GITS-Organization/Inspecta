using Inspecta.Application.Contracts.Clients;
using Inspecta.Application.Interfaces;
using Inspecta.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Manage client organizations and their contacts.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamOperations)]
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

    /// <summary>Get a client's previous inspection requests and certificates.</summary>
    [HttpGet("{clientId:guid}/history")]
    [ProducesResponseType<ClientHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientHistoryResponse>> GetHistory(Guid clientId, CancellationToken cancellationToken)
    {
        var history = await clientService.GetHistoryAsync(clientId, cancellationToken);
        return history is null ? NotFound() : Ok(history);
    }

    /// <summary>Create a client and its contacts.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.TenantManagement)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ClientResponse>> Create(CreateClientRequest request, CancellationToken cancellationToken)
    {
        var managerId = User.IsInRole("admin") ? request.ManagerId : User.GetTenantId();
        if (!User.IsInRole("admin") && managerId is null) return Unauthorized();
        var client = await clientService.CreateAsync(request, managerId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { clientId = client.Id }, client);
    }

    /// <summary>Update a client's profile, location, and business details.</summary>
    [HttpPut("{clientId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantManagement)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ClientResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientResponse>> Update(
        Guid clientId,
        UpdateClientRequest request,
        CancellationToken cancellationToken)
    {
        var client = await clientService.UpdateAsync(clientId, request, cancellationToken);
        return Ok(client);
    }

    /// <summary>Archive a client.</summary>
    [HttpDelete("{clientId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantManagement)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(Guid clientId, CancellationToken cancellationToken)
    {
        await clientService.ArchiveAsync(clientId, cancellationToken);
        return NoContent();
    }
}