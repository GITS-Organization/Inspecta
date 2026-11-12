using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Authorization;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>The technical managers of a company, for choosing who reviews a job.</summary>
[ApiController]
[Route("api/v1/company/technical-managers")]
public sealed class CompanyTechnicalManagersController(IReviewerService reviewers) : ControllerBase
{
    /// <summary>The active technical managers of the caller's company, by name; <c>isDefault</c> marks the default reviewer.</summary>
    /// <remarks>
    /// For the dropdown that picks the reviewer of a request (coordinator), of a direct certificate (inspector or technical manager) or of a man
    /// day. Every new request starts with the default one. The platform owner names the company with <c>companyId</c>; without it the list is empty.
    /// Only <c>id</c>, <c>fullName</c>, <c>email</c> and <c>isDefault</c>.
    /// </remarks>
    [HttpGet]
    [RequirePermission(Permissions.TeamReviewers)]
    [ProducesResponseType<IReadOnlyList<ReviewerResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReviewerResponse>>> List([FromQuery] Guid? companyId, CancellationToken cancellationToken)
    {
        var role = User.FindFirst("role")?.Value ?? string.Empty;
        if (!Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var actorId)) return Unauthorized();
        return Ok(await reviewers.ListAsync(actorId, role, companyId, cancellationToken));
    }
}
