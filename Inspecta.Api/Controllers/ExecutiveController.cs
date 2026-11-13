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
    /// The Renewals screen: machines due for inspection are the next work. <c>buckets</c> (overdue, and cumulative from today: 7, 30, 60, 90 days),
    /// <c>withJobCount</c> (due in 30 days and already on an open request), <c>registered</c>, <c>safePercent</c>, <c>weekly</c> (overdue, then eleven weeks),
    /// <c>mostOverdueByClient</c> and the machines (<c>filter</c>: <c>all</c>, <c>overdue</c>, <c>soon</c> (30 days), <c>later</c>, <c>notSafe</c>; paged,
    /// <c>pageSize</c> default 50, max 100). Only active machines that have a next inspection date count. <c>reviewerId</c> does not apply here.
    /// </summary>
    [RequirePermission(Permissions.ExecutiveView)]
    [HttpGet("renewals")]
    [ProducesResponseType<RenewalsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RenewalsResponse>> Renewals(
        [FromQuery] string? filter, [FromQuery] Guid? managerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(await executive.RenewalsAsync(new ExecutiveQuery(null, null, ResolveCompany(managerId, required: false), null), filter, page, pageSize, cancellationToken));

    /// <summary>
    /// The Clients screen: <c>counts</c> (active, with a contract, healthy, watch, at risk; they ignore the filters) and the clients, biggest invoiced year to
    /// date first. <c>health</c>: <c>healthy</c>, <c>watch</c> or <c>at_risk</c>. <c>q</c> searches name and city. Paged, <c>pageSize</c> default 50, max 100.
    /// </summary>
    [RequirePermission(Permissions.ExecutiveView)]
    [HttpGet("clients")]
    [ProducesResponseType<ClientsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ClientsResponse>> Clients(
        [FromQuery] string? health, [FromQuery] string? q, [FromQuery] Guid? managerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(await executive.ClientsAsync(new ExecutiveQuery(null, null, ResolveCompany(managerId, required: false), null), health, q, page, pageSize, cancellationToken));

    /// <summary>One client: its row, contacts, the machines due within 90 days or overdue, its last 20 invoices and its money. <c>404</c> for a client that does not exist or is not the company's.</summary>
    [RequirePermission(Permissions.ExecutiveView)]
    [HttpGet("clients/{clientId:guid}")]
    [ProducesResponseType<ClientDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDetailResponse>> Client(Guid clientId, [FromQuery] Guid? managerId, CancellationToken cancellationToken) =>
        await executive.ClientAsync(new ExecutiveQuery(null, null, ResolveCompany(managerId, required: false), null), clientId, cancellationToken) is { } client ? Ok(client) : NotFound();

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
