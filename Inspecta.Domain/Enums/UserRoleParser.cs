namespace Inspecta.Domain.Enums;

public static class UserRoleParser
{
    public static UserRole Parse(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role is required.", nameof(role));

        return role.Trim().ToLowerInvariant() switch
        {
            "coordinator" or "supervisor" => UserRole.Coordinator,
            "inspector" => UserRole.Inspector,
            "technical_manager" => UserRole.TechnicalManager,
            "admin" => UserRole.Admin,
            _ => throw new ArgumentException($"Unsupported user role '{role}'.", nameof(role))
        };
    }
}
