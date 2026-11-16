using Inspecta.Application.Authorization;
using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Email;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Configure IMAP access for a coordinator mailbox.</summary>
[ApiController]
[Route("api/v1/company/users/{coordinatorId:guid}/email-config")]
[Route("api/v1/manager/team/{coordinatorId:guid}/email-config")]
public sealed class CoordinatorEmailConfigsController(
    ICoordinatorEmailConfigService configService) : ControllerBase
{
    /// <summary>Replace the encrypted IMAP settings for a coordinator in the selected team.</summary>
    [RequirePermission(Permissions.AccountsManage)]
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid coordinatorId,
        CoordinatorEmailConfigRequest request,
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken)
    {
        var targetManagerId = ResolveManagerId(managerId);
        await configService.UpdateAsync(
            targetManagerId,
            coordinatorId,
            request,
            cancellationToken);
        return NoContent();
    }

    /// <summary>Remove the coordinator's mailbox settings, whether a Microsoft sign-in or a password.</summary>
    /// <remarks>The mailbox is no longer read or sent from. <c>404 email_config_not_found</c> when none was set.</remarks>
    [RequirePermission(Permissions.AccountsManage)]
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid coordinatorId, [FromQuery] Guid? managerId, CancellationToken cancellationToken)
    {
        await configService.DeleteAsync(ResolveManagerId(managerId), coordinatorId, cancellationToken);
        return NoContent();
    }

    /// <summary>The coordinator's mailbox settings (never the password) and the date mail is imported from.</summary>
    /// <remarks><c>configured: false</c> when no mailbox was set yet; <c>syncFrom</c> is then the default, 1 November 2026.</remarks>
    [RequirePermission(Permissions.AccountsManage)]
    [HttpGet]
    [ProducesResponseType<CoordinatorEmailConfigResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CoordinatorEmailConfigResponse>> Get(
        Guid coordinatorId, [FromQuery] Guid? managerId, CancellationToken cancellationToken) =>
        Ok(await configService.GetAsync(ResolveManagerId(managerId), coordinatorId, cancellationToken));

    /// <summary>Choose the date mail is imported from.</summary>
    /// <remarks>
    /// Only mail that arrived after <c>syncFrom</c> is fetched; changing it makes the import start again from the new date (mail already
    /// imported is not imported twice). It does not delete anything: requests that were already imported stay.
    /// </remarks>
    [RequirePermission(Permissions.AccountsManage)]
    [HttpPut("sync-from")]
    [ProducesResponseType<CoordinatorEmailConfigResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CoordinatorEmailConfigResponse>> ChangeSyncFrom(
        Guid coordinatorId, ChangeSyncFromRequest request, [FromQuery] Guid? managerId, CancellationToken cancellationToken) =>
        Ok(await configService.ChangeSyncFromAsync(ResolveManagerId(managerId), coordinatorId, request.SyncFrom, cancellationToken));

    /// <summary>Try the settings typed in, without saving anything: can the server be reached, does the sign-in work, can the inbox be read, can mail be sent.</summary>
    /// <remarks>
    /// The same body as the save call (the password is needed). Always answers <c>200</c> with the four steps (<c>reach</c>, <c>sign_in</c>, <c>read</c>, <c>send</c>);
    /// a step is <c>ok</c>, <c>failed</c> (with <c>message</c> and <c>hint</c> in plain words) or <c>skipped</c> after an earlier failure. <c>ok</c> is true only when all four worked.
    /// No email is sent and nothing is stored. Trying a mailbox is limited to ten tries a minute for a company, whichever way (test, check, or save/create with <c>verify</c>): <c>429 too_many_tries</c> after that.
    /// </remarks>
    [RequirePermission(Permissions.AccountsManage)]
    [HttpPost("test")]
    [ProducesResponseType<MailboxCheckResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MailboxCheckResponse>> Test(
        Guid coordinatorId, CoordinatorEmailConfigRequest request, [FromQuery] Guid? managerId, CancellationToken cancellationToken) =>
        Ok(await configService.TestAsync(ResolveManagerId(managerId), coordinatorId, request, cancellationToken));

    /// <summary>Try the saved mailbox now and remember the result.</summary>
    /// <remarks>
    /// The same four steps as the test, run on the saved settings (a Microsoft mailbox signs in with its saved sign-in). The result is kept: <c>connectionStatus</c>
    /// becomes <c>connected</c> or <c>problem</c> (<c>needs_reconnect</c> when Microsoft refuses the sign-in), <c>lastCheckedAt</c> moves to now and <c>lastAuthError</c> says why.
    /// <c>404 email_config_not_found</c> when no mailbox is set up.
    /// </remarks>
    [RequirePermission(Permissions.AccountsManage)]
    [HttpPost("check")]
    [ProducesResponseType<MailboxCheckResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MailboxCheckResponse>> Check(Guid coordinatorId, [FromQuery] Guid? managerId, CancellationToken cancellationToken) =>
        Ok(await configService.CheckAsync(ResolveManagerId(managerId), coordinatorId, cancellationToken));

    private Guid ResolveManagerId(Guid? requestedManagerId)
    {
        var actorId = GetActorId();
        if (User.IsInRole("admin"))
            return requestedManagerId
                ?? throw new AuthException("manager_required", "Admins must provide a managerId.", 400);

        var companyId = User.GetTenantId() ?? throw new AuthException("forbidden", "The account does not belong to a company.", 403);
        if (requestedManagerId is Guid managerId && managerId != companyId)
            throw new AuthException("forbidden", "You can only manage your own company.", 403);
        return companyId;
    }

    private Guid GetActorId()
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(subject, out var actorId)
            ? actorId
            : throw new AuthException("unauthorized", "Authenticated user id is invalid.");
    }
}
