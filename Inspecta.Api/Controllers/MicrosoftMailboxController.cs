using Inspecta.Api.Authorization;
using Inspecta.Application.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Inspecta.Api.Controllers;

/// <summary>A coordinator connects their Outlook / Microsoft 365 mailbox by signing in to Microsoft.</summary>
[ApiController]
[Route("api/v1")]
public sealed class MicrosoftMailboxController(
    IMicrosoftMailboxConnectionService connectionService,
    ICoordinatorEmailConfigService configService) : ControllerBase
{
    public const string CallbackRateLimitPolicy = "microsoft-callback";

    /// <summary>The signed-in coordinator's own mailbox settings and connection status.</summary>
    /// <remarks>
    /// <c>authMode</c> is <c>password</c> or <c>microsoft_oauth</c>; <c>connectionStatus</c> is <c>connected</c> or <c>needs_reconnect</c>
    /// (then <c>lastAuthError</c> says why and the coordinator connects again). Never holds a password or a token.
    /// </remarks>
    [RequirePermission(Permissions.MailboxConnectOwn)]
    [HttpGet("me/email-connection")]
    [ProducesResponseType<Inspecta.Application.Contracts.Email.CoordinatorEmailConfigResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Inspecta.Application.Contracts.Email.CoordinatorEmailConfigResponse>> Status(CancellationToken cancellationToken) =>
        Ok(await configService.GetOwnAsync(GetActorId(), cancellationToken));

    /// <summary>The Microsoft sign-in address to open in a browser.</summary>
    /// <remarks>
    /// For a signed-in coordinator, for their own mailbox. After signing in Microsoft sends the browser to the callback,
    /// which saves the connection and shows a plain page. <c>503 microsoft_not_configured</c> when the server has no Microsoft app settings.
    /// </remarks>
    [RequirePermission(Permissions.MailboxConnectOwn)]
    [HttpPost("me/email-connection/microsoft/start")]
    [ProducesResponseType<StartMicrosoftConnectionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<StartMicrosoftConnectionResponse>> Start(CancellationToken cancellationToken) =>
        Ok(await connectionService.StartAsync(GetActorId(), cancellationToken));

    /// <summary>Where Microsoft sends the browser back after the coordinator signed in. Not called by the apps.</summary>
    /// <remarks>Public on purpose: only a valid, unexpired <c>state</c> (made by <c>start</c>) does anything. Answers a plain HTML page.</remarks>
    [HttpGet("email-connection/microsoft/callback")]
    [AllowAnonymous]
    [EnableRateLimiting(CallbackRateLimitPolicy)]
    [Produces("text/html")]
    public async Task<ContentResult> Callback(
        [FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        var result = await connectionService.CompleteAsync(state, code, error, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        var title = result.Connected ? "Mailbox connected" : "The mailbox was not connected";
        var detail = result.Code is null ? string.Empty : $"<p class=\"code\">{WebUtility.HtmlEncode(result.Code)}</p>";
        return new ContentResult
        {
            StatusCode = result.Connected ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest,
            ContentType = "text/html; charset=utf-8",
            Content =
                "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
                $"<title>{WebUtility.HtmlEncode(title)}</title>" +
                "<style>body{font-family:system-ui,sans-serif;margin:0;display:grid;place-items:center;min-height:100vh;background:#f5f7fb;color:#13285d}" +
                "main{max-width:28rem;padding:2rem;background:#fff;border-radius:12px;box-shadow:0 2px 12px rgba(0,0,0,.08)}" +
                "h1{font-size:1.25rem;margin:0 0 .75rem}p{margin:.5rem 0;line-height:1.5}.code{font-family:monospace;font-size:.8rem;color:#5b6b85}</style></head>" +
                $"<body><main><h1>{WebUtility.HtmlEncode(title)}</h1><p>{WebUtility.HtmlEncode(result.Message)}</p>{detail}</main></body></html>"
        };
    }

    private Guid GetActorId()
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(subject, out var actorId)
            ? actorId
            : throw new AuthException("unauthorized", "Authenticated user id is invalid.");
    }
}
