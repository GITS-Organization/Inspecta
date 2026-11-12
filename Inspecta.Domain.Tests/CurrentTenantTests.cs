using System.Security.Claims;
using Inspecta.Application.Authorization;
using Inspecta.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace Inspecta.Domain.Tests;

/// <summary>
/// The platform owner is the only one who sees across companies: <see cref="CurrentTenant.IsAdmin"/> switches the tenant filters off, so no
/// company role may ever pass it, whatever it is called.
/// </summary>
public sealed class CurrentTenantTests
{
    private static CurrentTenant TenantOf(string role, Guid? company = null)
    {
        var claims = new List<Claim> { new("role", role) };
        if (company is { } id) claims.Add(new Claim("tenant_id", id.ToString()));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", "name", "role")) }
        };
        return new CurrentTenant(accessor);
    }

    [Fact]
    public void OnlyThePlatformOwnerBypassesTheTenantFilters()
    {
        foreach (var role in RoleNames.All)
            Assert.Equal(role == RoleNames.Admin, TenantOf(role).IsAdmin);
        Assert.False(TenantOf("company_admin_plus").IsAdmin);
        Assert.False(TenantOf("ADMIN").IsAdmin);
    }

    [Fact]
    public void TheTenantIsTheCompanyOfTheToken()
    {
        var company = Guid.NewGuid();

        Assert.Equal(company, TenantOf(RoleNames.CompanyAdmin, company).TenantId);
        Assert.Null(TenantOf(RoleNames.Admin).TenantId);
    }
}
