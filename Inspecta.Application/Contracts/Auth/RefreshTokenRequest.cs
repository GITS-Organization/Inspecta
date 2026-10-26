using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Auth;

/// <summary>Refresh token used to rotate or revoke an authenticated session.</summary>
public sealed record RefreshTokenRequest
{
	[Required]
	public required string RefreshToken { get; init; }
}
