using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>The company logo, so the email preview of the app can show the signature as the client will see it.</summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)] // the preview's HTML points at it; it is not a step of the workflow
[Route("api/v1/branding")]
public sealed class BrandingController(IEmailBranding branding) : ControllerBase
{
    [HttpGet("logo.png")]
    [ResponseCache(Duration = 86400)]
    [Produces("image/png")]
    public IActionResult Logo() =>
        branding.Get().LogoPng is { Length: > 0 } logo ? File(logo, "image/png") : NotFound();
}
