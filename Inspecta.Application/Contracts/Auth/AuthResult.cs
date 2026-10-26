namespace Inspecta.Application.Contracts.Auth;

/// <summary>Access and refresh tokens with the authenticated user's profile.</summary>
public sealed record AuthResult(
	string AccessToken,
	string RefreshToken,
	DateTimeOffset ExpiresAt,
	AuthUserResponse User);
