using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/v1/users")]
public sealed class UsersController(IUserManagementService userManagementService) : ControllerBase
{
    [HttpGet("{userId:guid}")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthUserResponse>> GetById(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManagementService.GetByIdAsync(userId, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthUserResponse>> Create(CreateStaffUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userManagementService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { userId = user.Id }, user);
    }

    [HttpPut("{userId:guid}/profile")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthUserResponse>> UpdateProfile(Guid userId, UpdateStaffProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await userManagementService.UpdateProfileAsync(userId, request, cancellationToken));

    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid userId, CancellationToken cancellationToken)
    {
        await userManagementService.DeactivateAsync(userId, cancellationToken);
        return NoContent();
    }
}