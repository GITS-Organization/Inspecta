using Inspecta.Application.Authorization;
using System.IdentityModel.Tokens.Jwt;
using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Operations for a technical manager's own team.</summary>
[ApiController]
[Route("api/v1/company/users")]
[Route("api/v1/manager/team")]
public sealed class ManagerTeamController(IUserManagementService userManagementService) : ControllerBase
{
    /// <summary>List coordinators and inspectors assigned to the current manager.</summary>
    [RequirePermission(Permissions.AccountsManage)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AuthUserResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuthUserResponse>>> List(
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken) =>
        Ok(await userManagementService.ListTeamAsync(ResolveManagerId(managerId), cancellationToken));

    /// <summary>Create a coordinator or inspector assigned to the current manager.</summary>
    [RequirePermission(Permissions.AccountsManage)]
    [HttpPost]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AuthUserResponse>> Create(
        CreateTeamMemberRequest request,
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken)
    {
        var targetManagerId = ResolveManagerId(managerId);
        var user = await userManagementService.CreateTeamMemberAsync(targetManagerId, request, cancellationToken, GetActorId());
        return CreatedAtAction(nameof(GetById), new { userId = user.Id, managerId = targetManagerId }, user);
    }

    /// <summary>Get a team member assigned to the current manager.</summary>
    [RequirePermission(Permissions.AccountsManage)]
    [HttpGet("{userId:guid}")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthUserResponse>> GetById(
        Guid userId,
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken)
    {
        var user = await userManagementService.GetTeamMemberAsync(ResolveManagerId(managerId), userId, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Update a team member's profile without changing their role or manager.</summary>
    [RequirePermission(Permissions.AccountsManage)]
    [HttpPut("{userId:guid}")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthUserResponse>> Update(
        Guid userId,
        UpdateTeamMemberRequest request,
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken) =>
        Ok(await userManagementService.UpdateTeamMemberAsync(ResolveManagerId(managerId), userId, request, cancellationToken));

    /// <summary>Enable or disable a team member without deleting the account.</summary>
    [RequirePermission(Permissions.AccountsManage)]
    [HttpPut("{userId:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        Guid userId,
        SetUserStatusRequest request,
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken)
    {
        await userManagementService.SetTeamMemberActiveAsync(
            ResolveManagerId(managerId),
            userId,
            request.IsActive,
            cancellationToken);
        return NoContent();
    }

    /// <summary>Set the password of a coordinator or inspector of the team. They are signed out on every device.</summary>
    /// <remarks>The password must follow the policy (12 characters or more, upper and lower case, a digit and a symbol): <c>400 password_policy_failed</c> otherwise. A lock caused by wrong guesses is lifted.</remarks>
    [RequirePermission(Permissions.AccountsManage)]
    [HttpPut("{userId:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPassword(
        Guid userId,
        SetPasswordRequest request,
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken)
    {
        await userManagementService.SetTeamMemberPasswordAsync(ResolveManagerId(managerId), userId, request.NewPassword, cancellationToken);
        return NoContent();
    }

    /// <summary>Change the sign-in email of a member of the team. They are signed out on every device and sign in with the new address.</summary>
    /// <remarks><c>409 account_exists</c> when the address belongs to another account, <c>400 invalid_email</c> when it is not an email.</remarks>
    [RequirePermission(Permissions.AccountsManage)]
    [HttpPut("{userId:guid}/email")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthUserResponse>> SetEmail(
        Guid userId,
        SetEmailRequest request,
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken) =>
        Ok(await userManagementService.ChangeTeamMemberEmailAsync(ResolveManagerId(managerId), userId, request.Email, cancellationToken));

    /// <summary>Delete a team member for good. <c>409 user_in_use</c> while records still point at them: deactivate instead.</summary>
    [RequirePermission(Permissions.AccountsManage)]
    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(
        Guid userId,
        [FromQuery] Guid? managerId,
        CancellationToken cancellationToken)
    {
        await userManagementService.DeactivateTeamMemberAsync(ResolveManagerId(managerId), userId, cancellationToken);
        return NoContent();
    }

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
