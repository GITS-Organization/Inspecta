using Inspecta.Application.Authorization;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Dashboard;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>The overview screen of the technical manager.</summary>
[ApiController]
[Route("api/v1/dashboard")]
public sealed class DashboardController(IDashboardService dashboard) : ControllerBase
{
    /// <summary>Everything the overview screen shows, in one call.</summary>
    /// <remarks>
    /// <c>pipeline</c>: the real requests of the team by status (all statuses, as of today); the entry <c>request_received</c> is the total of the requests that reached
    /// the offer stage or went on after it (every status from <c>quote_sent</c> to <c>sent_to_client</c>, except <c>quote_rejected</c>), not the ones still being received. <c>attention</c>: items overdue or due in the next 30 days,
    /// and jobs waiting for the manager's review. <c>certificates</c>: certificates approved (issued) this month, last month and per month for the chart
    /// (default: the last 7 months), split by verdict. <c>equipmentDue</c>: the first 10 items overdue or due in 30 days, overdue first; when there are none,
    /// the 10 items with the nearest due date after that (<c>mode</c> is <c>due_soon</c>, <c>upcoming</c> or <c>none</c>).
    /// <c>inspectorWorkload</c>: for each active inspector in the period (default: the last 30 days), <c>certificates</c> approved and <c>quotationsDone</c> (the jobs
    /// assigned to them that the technical manager approved in the period: a job counts at the moment it is approved), most certificates first.
    /// <para>
    /// <c>clientId</c> and <c>inspectorId</c> narrow every figure they apply to (the inspector filter does not apply to equipment). <c>from</c> and <c>to</c>
    /// (<c>yyyy-MM-dd</c>, both or neither, at most two years) set the period of the workload and the chart; the pipeline, the attention counts and the
    /// equipment are always as of today. Months are counted in UTC. A technical manager gets their own team; Admin may pass <c>managerId</c> (or none, for every team).
    /// </para>
    /// </remarks>
    [RequirePermission(Permissions.DashboardView)]
    [HttpGet("overview")]
    [ProducesResponseType<DashboardOverviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardOverviewResponse>> Overview(
        [FromQuery] Guid? managerId,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? inspectorId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        // A technical manager is always limited to their own team, whatever managerId says.
        var team = User.IsInRole("admin") ? managerId : User.GetTenantId();
        if (!User.IsInRole("admin") && team is null) return Forbid();
        return Ok(await dashboard.GetOverviewAsync(team, clientId, inspectorId, from, to, cancellationToken));
    }
}
