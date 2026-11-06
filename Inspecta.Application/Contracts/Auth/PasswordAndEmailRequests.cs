using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Auth;

/// <summary>A signed-in user choosing a new password for themselves.</summary>
public sealed record ChangeMyPasswordRequest
{
    [Required, MaxLength(256)]
    public required string CurrentPassword { get; init; }

    [Required, MaxLength(256)]
    public required string NewPassword { get; init; }

    /// <summary>
    /// The refresh token of the device being used. Every other device is signed out; this one stays signed in.
    /// Leave it out to sign out every device, this one included.
    /// </summary>
    [MaxLength(512)]
    public string? RefreshToken { get; init; }
}

/// <summary>A manager or an admin setting somebody else's password.</summary>
public sealed record SetPasswordRequest
{
    [Required, MaxLength(256)]
    public required string NewPassword { get; init; }
}

/// <summary>A manager or an admin correcting somebody else's sign-in email.</summary>
public sealed record SetEmailRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }
}
