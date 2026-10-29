using Inspecta.Application.Contracts.Equipment;
using Inspecta.Application.Interfaces;
using Inspecta.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Manage inspection items registered for clients.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamOperations)]
[Route("api/v1/equipment")]
public sealed class EquipmentController(IEquipmentService equipmentService) : ControllerBase
{
    /// <summary>List items, optionally filtered by client and archive status.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<EquipmentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EquipmentResponse>>> List([FromQuery] Guid? clientId, [FromQuery] bool includeArchived, CancellationToken cancellationToken) =>
        Ok(await equipmentService.ListAsync(clientId, includeArchived, cancellationToken));

    /// <summary>Get an item by identifier.</summary>
    [HttpGet("{equipmentId:guid}")]
    [ProducesResponseType<EquipmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EquipmentResponse>> GetById(Guid equipmentId, CancellationToken cancellationToken)
    {
        var equipment = await equipmentService.GetByIdAsync(equipmentId, cancellationToken);
        return equipment is null ? NotFound() : Ok(equipment);
    }

    /// <summary>Create an item for a client.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.TenantManagement)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<EquipmentResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EquipmentResponse>> Create(SaveEquipmentRequest request, CancellationToken cancellationToken)
    {
        var equipment = await equipmentService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { equipmentId = equipment.Id }, equipment);
    }

    /// <summary>Update an item's registration and metadata.</summary>
    [HttpPut("{equipmentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantManagement)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<EquipmentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EquipmentResponse>> Update(Guid equipmentId, SaveEquipmentRequest request, CancellationToken cancellationToken) =>
        Ok(await equipmentService.UpdateAsync(equipmentId, request, cancellationToken));

    /// <summary>Record inspection dates for an item.</summary>
    [HttpPost("{equipmentId:guid}/inspections")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RecordInspection(Guid equipmentId, RecordEquipmentInspectionRequest request, CancellationToken cancellationToken)
    {
        await equipmentService.RecordInspectionAsync(equipmentId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Archive an item.</summary>
    [HttpDelete("{equipmentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.TenantManagement)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(Guid equipmentId, CancellationToken cancellationToken)
    {
        await equipmentService.ArchiveAsync(equipmentId, cancellationToken);
        return NoContent();
    }
}