namespace Inspecta.Application.Contracts.Auth;

public sealed record AuthResult(
	string AccessToken,
	string RefreshToken,
	DateTimeOffset ExpiresAt,
	AuthUserResponse User);
