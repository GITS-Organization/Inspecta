using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>The company logo, so the email preview of the app can show the signature as the client will see it.</summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)] // the preview's HTML points at it; it is not a step of the workflow
[Route("api/v1/branding")]
public sealed class BrandingController(IEmailBranding branding, ICompanyProfileService profiles) : ControllerBase
{
    /// <summary>The logo of a team's company (the emails of that team show it); <c>404</c> when it has none.</summary>
    [HttpGet("{managerId:guid}/logo.png")]
    [ResponseCache(Duration = 3600)]
    [Produces("image/png")]
    public async Task<IActionResult> TeamLogo(Guid managerId, CancellationToken cancellationToken) =>
        await profiles.GetLogoAsync(managerId, cancellationToken) is { Length: > 0 } logo ? File(logo, "image/png") : NotFound();

    [HttpGet("logo.png")]
    [ResponseCache(Duration = 86400)]
    [Produces("image/png")]
    public IActionResult Logo() =>
        branding.Get().LogoPng is { Length: > 0 } logo ? File(logo, "image/png") : NotFound();
}
