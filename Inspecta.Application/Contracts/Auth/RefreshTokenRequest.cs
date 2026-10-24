using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Auth;

public sealed record RefreshTokenRequest
{
	[Required]
	public required string RefreshToken { get; init; }
}
