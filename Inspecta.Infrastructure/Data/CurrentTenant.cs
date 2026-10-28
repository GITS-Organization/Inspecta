using Microsoft.AspNetCore.Http;

namespace Inspecta.Infrastructure.Data;

public sealed class CurrentTenant(IHttpContextAccessor httpContextAccessor)
{
    public Guid? TenantId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst("tenant_id")?.Value, out var tenantId)
            ? tenantId
            : null;

    public bool IsAdmin =>
        string.Equals(
            httpContextAccessor.HttpContext?.User.FindFirst("role")?.Value,
            "admin",
            StringComparison.Ordinal);
}
