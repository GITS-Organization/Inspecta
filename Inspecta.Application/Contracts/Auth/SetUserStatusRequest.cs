namespace Inspecta.Application.Contracts.Auth;

/// <summary>Enable or disable an account without deleting its data.</summary>
public sealed record SetUserStatusRequest(bool IsActive);
