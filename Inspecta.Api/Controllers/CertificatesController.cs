using System.IdentityModel.Tokens.Jwt;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Manage inspection certificates associated with a request.</summary>
[ApiController]
[Authorize]
[Route("api/v1/requests/{requestId:guid}/certificate")]
public sealed class CertificatesController(ICertificateService certificateService) : ControllerBase
{
    /// <summary>Get the certificate for a request.</summary>
    [HttpGet]
    [ProducesResponseType<CertificateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CertificateResponse>> Get(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out var role)) return Unauthorized();
        var certificate = await certificateService.GetAsync(requestId, actorId, role, cancellationToken);
        return certificate is null ? NotFound() : Ok(certificate);
    }

    /// <summary>Save the inspector's certificate draft.</summary>
    [HttpPut("draft")]
    [Authorize(Roles = "admin,inspector")]
    [ProducesResponseType<CertificateResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CertificateResponse>> SaveDraft(Guid requestId, SaveCertificateDraftRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out _)) return Unauthorized();
        return Ok(await certificateService.SaveDraftAsync(requestId, actorId, request, cancellationToken));
    }

    /// <summary>Submit the certificate for technical review.</summary>
    [HttpPost("submit")]
    [Authorize(Roles = "admin,inspector")]
    [ProducesResponseType<CertificateResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CertificateResponse>> Submit(Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out _)) return Unauthorized();
        return Ok(await certificateService.SubmitAsync(requestId, actorId, cancellationToken));
    }

    /// <summary>Approve or return a submitted certificate.</summary>
    [HttpPost("review")]
    [Authorize(Roles = "admin,technical_manager")]
    [ProducesResponseType<CertificateResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CertificateResponse>> Review(Guid requestId, ReviewCertificateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId, out _)) return Unauthorized();
        return Ok(await certificateService.ReviewAsync(requestId, actorId, request, cancellationToken));
    }

    private bool TryGetActor(out Guid actorId, out string role)
    {
        role = User.FindFirst("role")?.Value ?? string.Empty;
        return Guid.TryParse(User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out actorId);
    }
}