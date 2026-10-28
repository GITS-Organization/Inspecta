using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Inspecta.Application.Exceptions;

namespace Inspecta.Api.Authorization;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirst("user_id")?.Value ??
            principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new AuthException("unauthorized", "Authenticated user id is invalid.");
    }

    public static Guid? GetTenantId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out var tenantId)
            ? tenantId
            : null;

    public static Guid? GetManagerId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst("manager_id")?.Value, out var managerId)
            ? managerId
            : null;
}
