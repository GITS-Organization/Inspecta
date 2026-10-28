using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Email;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Configure IMAP access for a coordinator mailbox.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TenantManagement)]
[Route("api/v1/manager/team/{coordinatorId:guid}/email-config")]
public sealed class CoordinatorEmailConfigsController(
    ICoordinatorEmailConfigService configService) : ControllerBase
{
    /// <summary>Replace the encrypted IMAP settings for a coordinator in the selected team.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid coordinatorId,
        CoordinatorEmailConfigRequest request,
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken)
    {
        var targetManagerId = ResolveManagerId(managerId);
        await configService.UpdateAsync(
            targetManagerId,
            coordinatorId,
            request,
            cancellationToken);
        return NoContent();
    }

    private Guid ResolveManagerId(Guid? requestedManagerId)
    {
        var actorId = GetActorId();
        if (User.IsInRole("admin"))
            return requestedManagerId
                ?? throw new AuthException("manager_required", "Admins must provide a managerId.", 400);

        if (requestedManagerId is Guid managerId && managerId != actorId)
            throw new AuthException("forbidden", "Technical managers can only manage their own team.", 403);
        return actorId;
    }

    private Guid GetActorId()
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(subject, out var actorId)
            ? actorId
            : throw new AuthException("unauthorized", "Authenticated user id is invalid.");
    }
}
