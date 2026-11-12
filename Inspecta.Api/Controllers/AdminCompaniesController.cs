using Inspecta.Api.Authorization;
using Inspecta.Application.Authorization;
using Inspecta.Application.Contracts.Companies;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>The companies of the platform: the platform owner creates one with its first account, switches it on or off and sets its subscription.</summary>
[ApiController]
[Route("api/v1/admin/companies")]
public sealed class AdminCompaniesController(ICompanyService companies) : ControllerBase
{
    /// <summary>Every company, by name.</summary>
    [HttpGet]
    [RequirePermission(Permissions.PlatformAdminister)]
    [ProducesResponseType<IReadOnlyList<CompanyResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanyResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await companies.ListAsync(cancellationToken));

    /// <summary>One company.</summary>
    [HttpGet("{companyId:guid}")]
    [RequirePermission(Permissions.PlatformAdminister)]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyResponse>> Get(Guid companyId, CancellationToken cancellationToken) =>
        await companies.GetAsync(companyId, cancellationToken) is { } company ? Ok(company) : NotFound();

    /// <summary>Create a company together with its first account, the company admin.</summary>
    /// <remarks>
    /// The company admin (<c>role: company_admin</c>) manages the company's identity (logo, accreditations, colours, contacts) and every other
    /// account of the company (coordinators, inspectors, technical managers, general and financial managers), and sees no business data. The
    /// first technical manager created in the company becomes its default reviewer. <c>subscriptionMonths</c> (1 to 120, default 12) sets the
    /// subscription. <c>409 account_exists</c> for an email already in use.
    /// </remarks>
    [HttpPost]
    [RequirePermission(Permissions.PlatformAdminister)]
    [ProducesResponseType<CreatedCompanyResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreatedCompanyResponse>> Create(CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        var created = await companies.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { companyId = created.Company.Id }, created);
    }

    /// <summary>Rename a company.</summary>
    [HttpPut("{companyId:guid}")]
    [RequirePermission(Permissions.PlatformAdminister)]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyResponse>> Rename(Guid companyId, RenameCompanyRequest request, CancellationToken cancellationToken) =>
        Ok(await companies.RenameAsync(companyId, request.Name, cancellationToken));

    /// <summary>Switch a company on or off. While it is off nobody in it can sign in and open sessions stop.</summary>
    [HttpPut("{companyId:guid}/status")]
    [RequirePermission(Permissions.PlatformAdminister)]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyResponse>> SetStatus(Guid companyId, SetCompanyStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await companies.SetStatusAsync(companyId, request.IsActive, cancellationToken));

    /// <summary>Set the subscription of a company (<c>active</c>, <c>suspended</c> or <c>expired</c>, with an optional end date).</summary>
    [HttpPut("{companyId:guid}/subscription")]
    [RequirePermission(Permissions.PlatformAdminister)]
    [ProducesResponseType<CompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyResponse>> UpdateSubscription(
        Guid companyId, UpdateCompanySubscriptionRequest request, CancellationToken cancellationToken) =>
        Ok(await companies.UpdateSubscriptionAsync(companyId, request, cancellationToken));
}
