using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Mail;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Admin maintenance for email requests that were imported in bulk by mistake.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v1/admin/email-backlog")]
public sealed class EmailBacklogController(IEmailBacklogService backlogService) : ControllerBase
{
    /// <summary>
    /// Permanently delete email requests imported before <c>createdBefore</c> that nobody started working on
    /// (still <c>request_received</c>, with no client, location, items or inspector). Anything a coordinator edited
    /// is kept. <c>dryRun</c> defaults to true: call it first to see how many would be deleted.
    /// </summary>
    [HttpPost("cleanup")]
    [ProducesResponseType<CleanupEmailBacklogResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CleanupEmailBacklogResponse>> Cleanup(
        CleanupEmailBacklogRequest request,
        CancellationToken cancellationToken) =>
        Ok(await backlogService.CleanupAsync(request, cancellationToken));
}
