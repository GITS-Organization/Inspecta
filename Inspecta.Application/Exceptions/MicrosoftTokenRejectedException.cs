namespace Inspecta.Application.Exceptions;

/// <summary>Microsoft refused a code or a refresh token (<c>invalid_grant</c>): it was revoked, expired or already used.</summary>
public sealed class MicrosoftTokenRejectedException(string message) : Exception(message);
