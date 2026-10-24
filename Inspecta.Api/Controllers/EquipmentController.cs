using Inspecta.Application.Contracts.Equipment;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin,coordinator")]
[Route("api/v1/equipment")]
public sealed class EquipmentController(IEquipmentService equipmentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<EquipmentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EquipmentResponse>>> List([FromQuery] Guid? clientId, [FromQuery] bool includeArchived, CancellationToken cancellationToken) =>
        Ok(await equipmentService.ListAsync(clientId, includeArchived, cancellationToken));

    [HttpGet("{equipmentId:guid}")]
    [ProducesResponseType<EquipmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EquipmentResponse>> GetById(Guid equipmentId, CancellationToken cancellationToken)
    {
        var equipment = await equipmentService.GetByIdAsync(equipmentId, cancellationToken);
        return equipment is null ? NotFound() : Ok(equipment);
    }

    [HttpPost]
    [ProducesResponseType<EquipmentResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EquipmentResponse>> Create(SaveEquipmentRequest request, CancellationToken cancellationToken)
    {
        var equipment = await equipmentService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { equipmentId = equipment.Id }, equipment);
    }

    [HttpPut("{equipmentId:guid}")]
    [ProducesResponseType<EquipmentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EquipmentResponse>> Update(Guid equipmentId, SaveEquipmentRequest request, CancellationToken cancellationToken) =>
        Ok(await equipmentService.UpdateAsync(equipmentId, request, cancellationToken));

    [HttpPost("{equipmentId:guid}/inspections")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RecordInspection(Guid equipmentId, RecordEquipmentInspectionRequest request, CancellationToken cancellationToken)
    {
        await equipmentService.RecordInspectionAsync(equipmentId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{equipmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(Guid equipmentId, CancellationToken cancellationToken)
    {
        await equipmentService.ArchiveAsync(equipmentId, cancellationToken);
        return NoContent();
    }
}