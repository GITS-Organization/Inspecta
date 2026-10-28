using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Manage inspection certificates associated with request items.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamAccess)]
[Route("api/v1/requests/{requestId:guid}/certificates")]
public sealed class CertificatesController(ICertificateService certificateService) : ControllerBase
{
    /// <summary>List certificates for all items in a request.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CertificateResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CertificateResponse>>> List(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var certificates = await certificateService.ListAsync(requestId, actorId, role, cancellationToken);
        return certificates is null ? NotFound() : Ok(certificates);
    }

    /// <summary>Get the certificate for one request item.</summary>
    [HttpGet("{requestItemId:guid}")]
    [ProducesResponseType<CertificateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CertificateResponse>> Get(
        Guid requestId,
        Guid requestItemId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var certificate = await certificateService.GetAsync(requestId, requestItemId, actorId, role, cancellationToken);
        return certificate is null ? NotFound() : Ok(certificate);
    }

    /// <summary>Create or update a certificate draft for a request item.</summary>
    [HttpPut("{requestItemId:guid}/draft")]
    [Authorize(Roles = "admin,technical_manager,inspector")]
    [ProducesResponseType<CertificateResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CertificateResponse>> SaveDraft(
        Guid requestId,
        Guid requestItemId,
        SaveCertificateDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await certificateService.SaveDraftAsync(requestId, requestItemId, actorId, role, request, cancellationToken));
    }

    /// <summary>Submit one item's certificate for technical review.</summary>
    [HttpPost("{requestItemId:guid}/submit")]
    [Authorize(Roles = "admin,technical_manager,inspector")]
    [ProducesResponseType<CertificateResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CertificateResponse>> Submit(
        Guid requestId,
        Guid requestItemId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        return Ok(await certificateService.SubmitAsync(requestId, requestItemId, actorId, role, cancellationToken));
    }

    /// <summary>Approve or return one submitted item certificate.</summary>
    [HttpPost("{requestItemId:guid}/review")]
    [Authorize(Policy = AuthorizationPolicies.TenantManagement)]
    [ProducesResponseType<CertificateResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CertificateResponse>> Review(
        Guid requestId,
        Guid requestItemId,
        ReviewCertificateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out _)) return Unauthorized();
        return Ok(await certificateService.ReviewAsync(requestId, requestItemId, actorId, request, cancellationToken));
    }

    private bool TryGetActor(out Guid actorId, out string role)
    {
        role = User.FindFirst("role")?.Value ?? string.Empty;
        return Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
    }
}