using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Admin operations for provisioning technical manager accounts and subscriptions.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v1/technical-managers")]
public sealed class TechnicalManagersController(IUserManagementService userManagementService) : ControllerBase
{
    /// <summary>Create a technical manager account with an initial subscription, and optionally its company identity.</summary>
    /// <remarks>
    /// <c>companyProfile</c> (same fields as <c>PUT /admin/managers/{managerId}/company-profile</c>: name, address, contacts, logo, accreditations, colours)
    /// sets the identity printed on the team's certificates, quotations and emails. It is checked first, so a mistake in it (<c>400</c>) creates no account.
    /// A profile with only <c>logoBase64</c>, <c>accreditationsBase64</c> or colours keeps the built-in company details (the first customer's) and changes just those.
    /// Left out, the team uses the built-in identity until a profile is set. A profile without its own logo or accreditation marks gets the built-in ones.
    /// </remarks>
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
