using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Admin-only company provisioning, subscription, and lifecycle operations.</summary>
[ApiController]
[Authorize(Roles = "admin")]
[Route("api/v1/companies")]
public sealed class CompaniesController(IUserManagementService userManagementService) : ControllerBase
{
    /// <summary>List companies.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CompanyResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanyResponse>>> List(
        [FromQuery] bool includeDeleted,
        CancellationToken cancellationToken) =>
        Ok(await userManagementService.ListCompaniesAsync(includeDeleted, cancellationToken));

    /// <summary>Get a company and its effective subscription state.</summary>
    [HttpGet("{companyId:guid}")]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyResponse>> GetById(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await userManagementService.GetCompanyAsync(companyId, cancellationToken);
        return company is null ? NotFound() : Ok(company);
    }

    /// <summary>Create a company together with its initial technical manager.</summary>
    [HttpPost]
    [ProducesResponseType<CompanyWithManagerResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CompanyWithManagerResponse>> Create(
        CreateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await userManagementService.CreateCompanyWithManagerAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { }, result);
    }

    /// <summary>Activate, suspend, or update a company's subscription end date.</summary>
    [HttpPut("{companyId:guid}/subscription")]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyResponse>> UpdateSubscription(
        Guid companyId,
        UpdateCompanySubscriptionRequest request,
        CancellationToken cancellationToken) =>
        Ok(await userManagementService.UpdateCompanySubscriptionAsync(companyId, request, cancellationToken));

    /// <summary>Soft-delete a company and revoke the refresh tokens of its users.</summary>
    [HttpDelete("{companyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid companyId, CancellationToken cancellationToken)
    {
        await userManagementService.DeleteCompanyAsync(companyId, cancellationToken);
        return NoContent();
    }
}
