using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Certificates;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Create a certificate directly, without a request or a quotation, and look up what the catalogue knows.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamAccess)]
[Route("api/v1/certifications")]
public sealed class CertificationsController(IDirectCertificationService directCertification) : ControllerBase
{
    /// <summary>Create a certificate job straight away: pick the client and its machines.</summary>
    /// <remarks>
    /// For the Create certification tab. The job starts <c>in_progress</c> with the caller as its inspector, so the screen continues
    /// with the usual machine endpoints (<c>GET/PUT /quotation-queue/{requestId}/machines/...</c>, then submit) and the technical
    /// manager reviews it like any other job. Each machine is a registered one (<c>equipmentId</c>) or a new one (registered for the
    /// client, except gears). Inspectors, the technical manager and the admin can use it.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<MachineListResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MachineListResponse>> Create(CreateCertificationRequest request, CancellationToken cancellationToken)
    {
        var role = User.FindFirst("role")?.Value ?? string.Empty;
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var actorId)) return Unauthorized();
        var job = await directCertification.CreateAsync(request, actorId, role, User.GetTenantId(), cancellationToken);
        return Created($"/api/v1/quotation-queue/{job.RequestId}/machines", job);
    }

    /// <summary>The kinds of machine the catalogue knows, with their standards and whether they are gears.</summary>
    [HttpGet("machine-types")]
    [ProducesResponseType<IReadOnlyList<MachineTypeResponse>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<MachineTypeResponse>> MachineTypes() => Ok(directCertification.MachineTypes());

    /// <summary>How much a printed certificate takes of serial numbers, to plan a split.</summary>
    [HttpGet("serial-limits")]
    [ProducesResponseType<SerialLimitsResponse>(StatusCodes.Status200OK)]
    public ActionResult<SerialLimitsResponse> SerialLimits() =>
        Ok(new SerialLimitsResponse(SerialNumbers.MaxCharacters, SerialNumbers.MaxLength, SerialNumbers.MaxCount));
}
