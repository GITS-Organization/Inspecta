using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Company;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Admin only: the company identity (name, address, contacts, logo, colours) printed for a team.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v1/admin/managers/{managerId:guid}/company-profile")]
public sealed class AdminCompanyProfilesController(ICompanyProfileService profiles) : ControllerBase
{
    /// <summary>The company profile of a team (a technical manager).</summary>
    /// <remarks><c>404</c> when the team has none: its certificates, quotations and emails then use the built-in profile.</remarks>
    [HttpGet]
    [ProducesResponseType<CompanyProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyProfileResponse>> Get(Guid managerId, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(managerId, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>Create or replace the company profile of a team.</summary>
    /// <remarks>
    /// <c>name</c> and <c>address</c> go together: give both to set the company's own details, or leave both out to keep the built-in details (the first
    /// customer's) and change only the images and colours (<c>usesBuiltInDetails</c> is then true). The text fields are replaced by what is sent. <c>logoBase64</c> and <c>accreditationsBase64</c> (PNG, at most 1 MB each) are kept when left
    /// out and removed when empty; a company without its own logo or accreditation marks gets the built-in ones. From then on the team's certificates, quotations and the signature of its emails carry this identity; the ones
    /// already issued do not change. A quotation of a team with a profile has no cover page and leaves out the pages of texts it does not have
    /// (about us, services, terms).
    /// </remarks>
    [HttpPut]
    [ProducesResponseType<CompanyProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyProfileResponse>> Save(
        Guid managerId, SaveCompanyProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await profiles.SaveAsync(managerId, request, cancellationToken));
}
