using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Admin-only operations for managing users and their technical manager assignments.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v1/users")]
public sealed class UsersController(IUserManagementService userManagementService) : ControllerBase
{
    /// <summary>List users, optionally filtering by technical manager, role, or active status.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AuthUserResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuthUserResponse>>> List(
        [FromQuery] Guid? managerId,
        [FromQuery] string? role,
        [FromQuery] bool? isActive,
        [FromQuery] bool includeDeleted,
        CancellationToken cancellationToken)
    {
        UserRole? parsedRole = role is null ? null : UserRoleParser.Parse(role);
        return Ok(await userManagementService.ListAsync(managerId, parsedRole, isActive, cancellationToken, includeDeleted));
    }

    /// <summary>Get a user's profile by identifier.</summary>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthUserResponse>> GetById(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManagementService.GetByIdAsync(userId, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Create an account for any supported role.</summary>
    [HttpPost]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthUserResponse>> Create(CreateStaffUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userManagementService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { userId = user.Id }, user);
    }

    /// <summary>Update a user's profile, role, manager assignment, and operational settings.</summary>
    [HttpPut("{userId:guid}/profile")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthUserResponse>> UpdateProfile(Guid userId, UpdateStaffProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await userManagementService.UpdateProfileAsync(userId, request, cancellationToken));

    /// <summary>Enable or disable a user account without deleting the user.</summary>
    [HttpPut("{userId:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(
        Guid userId,
        SetUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        await userManagementService.SetUserActiveAsync(userId, request.IsActive, cancellationToken);
        return NoContent();
    }

    /// <summary>Set anyone's password (a technical manager's included). They are signed out on every device.</summary>
    [HttpPut("{userId:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPassword(Guid userId, SetPasswordRequest request, CancellationToken cancellationToken)
    {
        await userManagementService.SetUserPasswordAsync(userId, request.NewPassword, cancellationToken);
        return NoContent();
    }

    /// <summary>Change anyone's sign-in email. They are signed out on every device. <c>409 account_exists</c> when the address is taken.</summary>
    [HttpPut("{userId:guid}/email")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthUserResponse>> SetEmail(Guid userId, SetEmailRequest request, CancellationToken cancellationToken) =>
        Ok(await userManagementService.ChangeUserEmailAsync(userId, request.Email, cancellationToken));

    /// <summary>Delete a user for good (their sessions go with them). <c>409 user_in_use</c> while records still point at the user: deactivate instead.</summary>
    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(Guid userId, CancellationToken cancellationToken)
    {
        await userManagementService.DeactivateAsync(userId, cancellationToken);
        return NoContent();
    }
}