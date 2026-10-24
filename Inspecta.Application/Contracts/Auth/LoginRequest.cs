using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Auth;

public sealed record LoginRequest
{
	[Required, EmailAddress, MaxLength(320)]
	public required string Email { get; init; }

	[Required, MinLength(1), MaxLength(256)]
	public required string Password { get; init; }
}
