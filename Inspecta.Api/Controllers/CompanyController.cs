using Inspecta.Api.Authorization;
using Inspecta.Application.Authorization;
using Inspecta.Application.Contracts.Companies;
using Inspecta.Application.Contracts.Company;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>The caller's own company: what it is, who reviews by default, and its identity (logo, accreditations, colours, contacts).</summary>
[ApiController]
[Route("api/v1/company")]
public sealed class CompanyController(ICompanyService companies, ICompanyProfileService profiles) : ControllerBase
{
    /// <summary>The caller's company. The platform owner names it with <c>companyId</c>.</summary>
    [HttpGet]
    [RequirePermission(Permissions.CompanyView)]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyResponse>> Get([FromQuery] Guid? companyId, CancellationToken cancellationToken) =>
        await companies.GetAsync(Resolve(companyId), cancellationToken) is { } company ? Ok(company) : NotFound();

    /// <summary>Choose the technical manager every new request of the company starts with.</summary>
    /// <remarks>The company admin or the platform owner. The reviewer must be an active technical manager of the company (<c>GET /company/technical-managers</c>); the coordinator can still change it per request.</remarks>
    [HttpPut("default-reviewer")]
    [RequirePermission(Permissions.CompanySetDefaultReviewer)]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyResponse>> SetDefaultReviewer(
        SetDefaultReviewerRequest request, [FromQuery] Guid? companyId, CancellationToken cancellationToken) =>
        Ok(await companies.SetDefaultReviewerAsync(Resolve(companyId), request.ReviewerId, cancellationToken));

    /// <summary>The company's identity: name, address, contacts, logo, accreditations image and colours (404 when it has none yet: the built-in one is used).</summary>
    [HttpGet("profile")]
    [RequirePermission(Permissions.CompanyEditIdentity)]
    [ProducesResponseType<CompanyProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyProfileResponse>> GetProfile([FromQuery] Guid? companyId, CancellationToken cancellationToken) =>
        await profiles.GetAsync(Resolve(companyId), cancellationToken) is { } profile ? Ok(profile) : NotFound();

    /// <summary>Save the company's identity (the logo and the accreditations image are base64 PNGs; a bad image or colour saves nothing).</summary>
    [HttpPut("profile")]
    [RequirePermission(Permissions.CompanyEditIdentity)]
    [ProducesResponseType<CompanyProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyProfileResponse>> SaveProfile(
        SaveCompanyProfileRequest request, [FromQuery] Guid? companyId, CancellationToken cancellationToken) =>
        Ok(await profiles.SaveAsync(Resolve(companyId), request, cancellationToken));

    // A company member always works on their own company; the platform owner must say which.
    private Guid Resolve(Guid? requested)
    {
        if (User.IsInRole(RoleNames.Admin))
            return requested ?? throw new AuthException("company_required", "The platform owner must provide companyId.", 400);
        var own = User.GetTenantId() ?? throw new AuthException("forbidden", "The account does not belong to a company.", 403);
        if (requested is { } other && other != own)
            throw new AuthException("forbidden", "You can only work on your own company.", 403);
        return own;
    }
}
