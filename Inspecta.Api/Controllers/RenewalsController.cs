using Inspecta.Api.Authorization;
using Inspecta.Application.Authorization;
using Inspecta.Application.Contracts.Renewals;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>
/// Renewals: the machines whose next inspection is coming up, grouped by client, so the coordinator can ask the client first and turn the answer into a request.
/// The platform owner names the company with <c>managerId</c>; everyone else always gets their own company.
/// </summary>
[ApiController]
[Route("api/v1/renewals")]
public sealed class RenewalsController(IRenewalsService renewals) : ControllerBase
{
    /// <summary>
    /// The clients that have machines due. <c>window</c>: <c>overdue</c>, <c>30</c> (default), <c>60</c> or <c>90</c> (days; the counts and the windows include the
    /// overdue machines). Each client lists every active machine that is overdue or due within 90 days, <c>inWindow</c> says which ones to tick, and
    /// <c>onOpenRequest</c> marks machines that are already on a request. Retired and archived machines never appear. <c>recipients</c> are the addresses a
    /// reminder may be sent to. <c>q</c> searches the client name; <c>pageSize</c> default 50, max 100.
    /// </summary>
    [RequirePermission(Permissions.RenewalsWork)]
    [HttpGet]
    [ProducesResponseType<RenewalsListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RenewalsListResponse>> List(
        [FromQuery] string? window, [FromQuery] string? q, [FromQuery] Guid? managerId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(await renewals.ListAsync(ResolveCompany(managerId), window, q, page, pageSize, cancellationToken));

    private Guid ResolveCompany(Guid? requested)
    {
        if (User.IsInRole("admin"))
            return requested ?? throw new AuthException("manager_required", "The platform owner must provide a managerId.", 400);
        return User.GetTenantId() ?? throw new AuthException("forbidden", "The account does not belong to a company.", 403);
    }
}
