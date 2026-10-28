using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

public sealed class TenantQueryFilterTests
{
    [Fact]
    public void TeamClientQueriesAreScopedToTheTenantClaim()
    {
        var tenantId = Guid.NewGuid();
        using var dbContext = CreateContext("coordinator", tenantId);

        var sql = dbContext.Clients.ToQueryString();

        Assert.Contains("ManagerId", sql, StringComparison.Ordinal);
        Assert.Contains(tenantId.ToString(), sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminQueriesKeepTheGlobalBypassEnabled()
    {
        using var dbContext = CreateContext("admin", null);

        var sql = dbContext.Clients.ToQueryString();

        Assert.Contains("ManagerId", sql, StringComparison.Ordinal);
        Assert.Contains("True", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EquipmentQueriesAreScopedThroughTheirClientTenant()
    {
        var tenantId = Guid.NewGuid();
        using var dbContext = CreateContext("inspector", tenantId);

        var sql = dbContext.Items.ToQueryString();

        Assert.Contains("Clients", sql, StringComparison.Ordinal);
        Assert.Contains("ManagerId", sql, StringComparison.Ordinal);
        Assert.Contains(tenantId.ToString(), sql, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestRelatedQueriesAreScopedThroughTheirParentRequest()
    {
        var tenantId = Guid.NewGuid();
        using var dbContext = CreateContext("coordinator", tenantId);
        var queries = new[]
        {
            dbContext.Requests.ToQueryString(),
            dbContext.RequestItems.ToQueryString(),
            dbContext.JobStatusHistory.ToQueryString(),
            dbContext.Quotes.ToQueryString(),
            dbContext.Certificates.ToQueryString(),
            dbContext.ClientContacts.ToQueryString()
        };

        Assert.All(queries, sql =>
        {
            Assert.Contains("ManagerId", sql, StringComparison.Ordinal);
            Assert.Contains(tenantId.ToString(), sql, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TeamAccessTokenContainsManagerAndTenantClaims()
    {
        var managerId = Guid.NewGuid();
        var user = AppUser.Create("coordinator@example.com", "Coordinator", UserRole.Coordinator, managerId: managerId);
        var token = new JwtAccessTokenService(CreateTokenConfiguration()).Create(user).Token;
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToArray();

        Assert.Contains(claims, claim => claim.Type == "user_id" && claim.Value == user.Id.ToString());
        Assert.Contains(claims, claim => claim.Type == "role" && claim.Value == "coordinator");
        Assert.Contains(claims, claim => claim.Type == "manager_id" && claim.Value == managerId.ToString());
        Assert.Contains(claims, claim => claim.Type == "tenant_id" && claim.Value == managerId.ToString());
    }

    [Fact]
    public void AdminAccessTokenDoesNotReceiveATenantClaim()
    {
        var user = AppUser.Create("admin@example.com", "Admin", UserRole.Admin);
        var token = new JwtAccessTokenService(CreateTokenConfiguration()).Create(user).Token;
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToArray();

        Assert.Contains(claims, claim => claim.Type == "role" && claim.Value == "admin");
        Assert.DoesNotContain(claims, claim => claim.Type == "tenant_id");
    }

    private static InspectaDbContext CreateContext(string role, Guid? tenantId)
    {
        var claims = new List<Claim> { new("role", role) };
        if (tenantId is Guid id)
            claims.Add(new Claim("tenant_id", id.ToString()));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var options = new DbContextOptionsBuilder<InspectaDbContext>()
            .UseNpgsql("Host=localhost;Database=inspecta_test;Username=test;Password=test")
            .Options;

        return new InspectaDbContext(options, new CurrentTenant(accessor));
    }

    private static IConfiguration CreateTokenConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "Inspecta.Tests",
            ["Jwt:Audience"] = "Inspecta.Tests",
            ["Jwt:SigningKey"] = "a-test-signing-key-that-is-at-least-32-bytes-long",
            ["Jwt:AccessTokenMinutes"] = "10"
        }).Build();
}
