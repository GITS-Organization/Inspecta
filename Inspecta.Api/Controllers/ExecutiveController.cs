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
    /// The Money screen. <c>kpis</c>: invoiced (invoices created in the period), collected (payments dated in it) with its rate and target, outstanding (the
    /// balance of invoices that are not closed, the part older than 60 days, and apart the balance of invoices closed by hand), days to collect with its target.
    /// <c>aging</c> by the age of the invoice, <c>whoOwesMost</c> (six clients) and <c>monthly</c> (the last twelve months). <c>from</c> and <c>to</c>
    /// (<c>yyyy-MM-dd</c>, default the first of the month to today, at most two years); <c>reviewerId</c> keeps the invoices of the requests that
    /// reviewer is set on; <c>managerId</c> only for the platform owner. The invoice list uses <c>GET /invoices</c>.
    /// </summary>
    [RequirePermission(Permissions.ExecutiveView)]
    [HttpGet("money")]
    [ProducesResponseType<MoneyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MoneyResponse>> Money(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? managerId, [FromQuery] Guid? reviewerId, CancellationToken cancellationToken) =>
        Ok(await executive.MoneyAsync(new ExecutiveQuery(from, to, ResolveCompany(managerId, required: false), reviewerId), cancellationToken));

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
