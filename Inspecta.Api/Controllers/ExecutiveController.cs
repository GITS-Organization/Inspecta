using Inspecta.Api.Authorization;
using Inspecta.Application.Authorization;
using Inspecta.Application.Contracts.Executive;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>
/// The general manager's screens: read-only figures for the whole company, with numbers, names, dates and statuses only (never what is inside a
/// request). The platform owner narrows to one company with <c>managerId</c>; everyone else always gets their own company.
/// </summary>
[ApiController]
[Route("api/v1/executive")]
public sealed class ExecutiveController(IExecutiveService executive) : ControllerBase
{
    /// <summary>
    /// The company's targets: the share of invoiced money collected (percent), the days to collect it, the days from request to delivery and the days
    /// a review should take. A company that never set them gets 85, 30, 28 and 2. The platform owner without <c>managerId</c> gets the defaults.
    /// </summary>
    [RequirePermission(Permissions.ExecutiveManageTargets)]
    [HttpGet("targets")]
    [ProducesResponseType<ExecutiveTargetsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ExecutiveTargetsResponse>> GetTargets([FromQuery] Guid? managerId, CancellationToken cancellationToken) =>
        Ok(await executive.GetTargetsAsync(ResolveCompany(managerId, required: false), cancellationToken));

    /// <summary>
    /// Replace the four targets (percent 1 to 100, days 1 to 365). The company admin changes their own company's; the platform owner must name the
    /// company with <c>managerId</c> (<c>400 manager_required</c>).
    /// </summary>
    [RequirePermission(Permissions.ExecutiveManageTargets)]
    [HttpPut("targets")]
    [ProducesResponseType<ExecutiveTargetsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ExecutiveTargetsResponse>> SaveTargets(
        [FromBody] SaveExecutiveTargetsRequest request, [FromQuery] Guid? managerId, CancellationToken cancellationToken) =>
        Ok(await executive.SaveTargetsAsync(ResolveCompany(managerId, required: true)!.Value, request, User.GetUserId(), cancellationToken));

    // The platform owner names the company (or none, to see all); anyone else is always limited to their own.
    private Guid? ResolveCompany(Guid? requested, bool required)
    {
        if (User.IsInRole("admin"))
            return requested ?? (required ? throw new AuthException("manager_required", "The platform owner must provide a managerId.", 400) : null);
        return User.GetTenantId() ?? throw new AuthException("forbidden", "The account does not belong to a company.", 403);
    }
}
