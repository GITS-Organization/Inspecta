using System.IdentityModel.Tokens.Jwt;
using Inspecta.Application.Contracts.Timesheets;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Manage inspection timesheets and their review workflow.</summary>
[ApiController]
[Authorize]
[Route("api/v1/requests/{requestId:guid}/timesheet")]
public sealed class TimesheetsController(ITimesheetService timesheetService) : ControllerBase
{
    /// <summary>Get a request's timesheet.</summary>
    [HttpGet]
    [ProducesResponseType<TimesheetResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TimesheetResponse>> Get(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var timesheet = await timesheetService.GetAsync(requestId, actorId, role, cancellationToken);
        return timesheet is null ? NotFound() : Ok(timesheet);
    }

    /// <summary>Create or update an inspector's timesheet.</summary>
    [HttpPut]
    [Authorize(Roles = "admin,inspector")]
    [ProducesResponseType<TimesheetResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TimesheetResponse>> Save(Guid requestId, SaveTimesheetRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out _)) return Unauthorized();
        return Ok(await timesheetService.SaveAsync(requestId, actorId, request, cancellationToken));
    }

    /// <summary>Approve or return a timesheet for correction.</summary>
    [HttpPost("review")]
    [Authorize(Roles = "admin,coordinator")]
    [ProducesResponseType<TimesheetResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TimesheetResponse>> Review(Guid requestId, ReviewTimesheetRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out _)) return Unauthorized();
        return Ok(await timesheetService.ReviewAsync(requestId, actorId, request, cancellationToken));
    }

    private bool TryGetActor(out Guid actorId, out string role)
    {
        role = User.FindFirst("role")?.Value ?? string.Empty;
        return Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
    }
}