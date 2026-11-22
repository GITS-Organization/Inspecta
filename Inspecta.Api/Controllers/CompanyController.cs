using Inspecta.Api.Authorization;
using Inspecta.Application.Authorization;
using Inspecta.Application.Contracts.Companies;
using Inspecta.Application.Contracts.Company;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>The caller's own company: what it is, who reviews by default, and its identity (logo, accreditations, colours, contacts).</summary>
[ApiController]
[Route("api/v1/company")]
public sealed class CompanyController(
    ICompanyService companies, ICompanyProfileService profiles, IQuotationContentService quotationContent, VerificationDomainService verificationDomains) : ControllerBase
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

    /// <summary>What the company prints in its quotations: the sections of its terms, its bank accounts and the contacts on the cover and in the footer.</summary>
    /// <remarks>
    /// The technical manager, the company admin or the platform owner (with <c>companyId</c>). Always 200: <c>isDefault</c> is true while nothing was saved, and the lists are then the
    /// default (the built-in content for the built-in company, empty for any other). A quotation that was already sent keeps what it was sent with.
    /// </remarks>
    [HttpGet("quotation-content")]
    [RequirePermission(Permissions.CompanyEditQuotationContent)]
    [ProducesResponseType<QuotationContentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotationContentResponse>> GetQuotationContent([FromQuery] Guid? companyId, CancellationToken cancellationToken) =>
        Ok(await quotationContent.GetAsync(Resolve(companyId), cancellationToken));

    /// <summary>Replace what the company prints in its quotations (everything is replaced by what is sent; the last save wins).</summary>
    /// <remarks>
    /// Limits: 12 terms sections (title up to 120 characters, 15 points of up to 600, at most one section with <c>showBankAccounts</c>), 5 bank accounts, 3 addresses, 3 phones and 3 emails.
    /// Anything over a limit is <c>400 bad_request</c> with a readable message and saves nothing. Only quotations sent afterwards use it; drafts show it at once.
    /// </remarks>
    [HttpPut("quotation-content")]
    [RequirePermission(Permissions.CompanyEditQuotationContent)]
    [ProducesResponseType<QuotationContentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuotationContentResponse>> SaveQuotationContent(
        SaveQuotationContentRequest request, [FromQuery] Guid? companyId, CancellationToken cancellationToken) =>
        Ok(await quotationContent.SaveAsync(Resolve(companyId), request, User.GetUserId(), cancellationToken));

    /// <summary>The addresses the company's certificates can point to (the QR code opens <c>https://{host}/verify/{code}</c>), the primary one first.</summary>
    /// <remarks>
    /// The technical manager, the company admin or the platform owner (with <c>companyId</c>). An empty list means the QR codes use the platform address.
    /// HTTPS for a new address is set up by us for now; it becomes automatic later.
    /// </remarks>
    [HttpGet("verification-domains")]
    [RequirePermission(Permissions.CompanyEditQuotationContent)]
    [ProducesResponseType<IReadOnlyList<VerificationDomainResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VerificationDomainResponse>>> GetVerificationDomains(
        [FromQuery] Guid? companyId, CancellationToken cancellationToken) =>
        Ok(await verificationDomains.ListAsync(Resolve(companyId), cancellationToken));

    /// <summary>Make an address the company's primary verification address: certificates issued from now on print a QR code that opens it.</summary>
    /// <remarks>
    /// Send only the host, for example <c>{"host":"verify.yourcompany.com"}</c> (no https://, path or port; letters, digits, hyphens and dots; not an address of the service).
    /// A new host is added; a host the company already has is made primary; the old primary stays in the list so QR codes already printed keep working.
    /// Errors: <c>400 invalid_host</c>, <c>400 host_reserved</c>, <c>409 host_taken</c> (another company uses it).
    /// </remarks>
    [HttpPut("verification-domain")]
    [RequirePermission(Permissions.CompanyEditQuotationContent)]
    [ProducesResponseType<VerificationDomainResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VerificationDomainResponse>> SetVerificationDomain(
        SetVerificationDomainRequest request, [FromQuery] Guid? companyId, CancellationToken cancellationToken) =>
        Ok(await verificationDomains.SetPrimaryAsync(Resolve(companyId), request.Host, cancellationToken));

    /// <summary>Remove an old verification address. The primary one cannot be removed (409 <c>primary_domain</c>): choose another primary first.</summary>
    [HttpDelete("verification-domains/{id:guid}")]
    [RequirePermission(Permissions.CompanyEditQuotationContent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteVerificationDomain(Guid id, [FromQuery] Guid? companyId, CancellationToken cancellationToken)
    {
        await verificationDomains.DeleteAsync(Resolve(companyId), id, cancellationToken);
        return NoContent();
    }

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
