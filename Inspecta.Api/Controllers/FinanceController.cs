using Inspecta.Api.Authorization;
using Inspecta.Application.Authorization;
using Inspecta.Application.Contracts.Executive;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>
/// The financial manager's Overview: every figure about money in the company, without what is inside a request. The invoices themselves (the list,
/// recording payments, closing) are the invoice endpoints.
/// </summary>
[ApiController]
[Route("api/v1/finance")]
public sealed class FinanceController(IExecutiveService executive) : ControllerBase
{
    /// <summary>
    /// <c>money</c> has the same figures as the general manager's Money screen: <c>kpis</c> (invoiced, collected with rate and target, outstanding, days to
    /// collect), <c>aging</c> by invoice age, <c>whoOwesMost</c> and <c>monthly</c> (twelve months). Then what to do: <c>readyToClose</c> (handed to finance and paid
    /// in full: the count, the total and the oldest five), <c>oldUnpaid</c> (the three oldest unpaid invoices of more than 60 days that are with finance),
    /// <c>notHandedOver</c> (invoices still with the coordinators and what they owe) and <c>latestPayments</c> (the last ten payments, with who recorded them).
    /// <c>from</c> and <c>to</c> (<c>yyyy-MM-dd</c>, default the first of the month to today, at most two years) set the period of the money figures;
    /// <c>managerId</c> is only for the platform owner.
    /// </summary>
    [RequirePermission(Permissions.FinanceView)]
    [HttpGet("overview")]
    [ProducesResponseType<FinanceOverviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FinanceOverviewResponse>> Overview(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? managerId, CancellationToken cancellationToken) =>
        Ok(await executive.FinanceOverviewAsync(new ExecutiveQuery(from, to, ResolveCompany(managerId), null), cancellationToken));

    // The platform owner names the company (or none, to see all); anyone else is always limited to their own.
    private Guid? ResolveCompany(Guid? requested) =>
        User.IsInRole("admin")
            ? requested
            : User.GetTenantId() ?? throw new AuthException("forbidden", "The account does not belong to a company.", 403);
}
