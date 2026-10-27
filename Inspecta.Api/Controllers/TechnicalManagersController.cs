using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Admin operations for provisioning technical manager accounts and subscriptions.</summary>
[ApiController]
[Authorize(Roles = "admin")]
[Route("api/v1/technical-managers")]
public sealed class TechnicalManagersController(IUserManagementService userManagementService) : ControllerBase
{
    /// <summary>Create a technical manager account with an initial subscription.</summary>
    [HttpPost]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthUserResponse>> Create(
        CreateTechnicalManagerRequest request,
        CancellationToken cancellationToken)
    {
        var manager = await userManagementService.CreateTechnicalManagerAsync(request, cancellationToken);
        return CreatedAtAction("GetById", "Users", new { userId = manager.Id }, manager);
    }

    /// <summary>Activate, suspend, or update a technical manager's subscription end date.</summary>
    [HttpPut("{managerId:guid}/subscription")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthUserResponse>> UpdateSubscription(
        Guid managerId,
        UpdateTechnicalManagerSubscriptionRequest request,
        CancellationToken cancellationToken) =>
        Ok(await userManagementService.UpdateTechnicalManagerSubscriptionAsync(
            managerId,
            request,
            cancellationToken));
}
