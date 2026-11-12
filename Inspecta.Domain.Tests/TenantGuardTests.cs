using System.Security.Claims;
using Inspecta.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Inspecta.Domain.Tests;

/// <summary>
/// Tenancy rule 1: an entity that carries a tenant column (<c>ManagerId</c> or <c>TenantId</c>) has a query filter that uses
/// it, so a forgotten <c>Where</c> in a repository cannot show one company's rows to another. The few entities without such a
/// filter are listed below with the reason; a new one needs a conscious edit here, in review.
/// </summary>
public sealed class TenantGuardTests : IDisposable
{
    /// <summary>Entities with a tenant column and no tenant filter, and why that is safe today.</summary>
    private static readonly IReadOnlyDictionary<string, string> Allowed = new Dictionary<string, string>
    {
        ["AppUser"] = "Holds the tenants themselves (a manager has no ManagerId) and sign-in by email across tenants; lists are scoped by ManagerId in EfUserRepository.",
        ["CertificateCounter"] = "One counter row per manager and year, always read and incremented by manager id when a certificate number is issued.",
        ["CompanyProfile"] = "The logo is public (it is in the emails and on certificates); the profile is always read by manager id.",
        ["CoordinatorEmailConfig"] = "Read by coordinator id after the team check in CoordinatorEmailConfigService; the mail sync reads all of them on purpose."
    };

    private readonly SqliteConnection connection = new("Data Source=:memory:");

    private IModel Model()
    {
        connection.Open();
        var options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "admin")], "test")) }
        };
        using var db = new InspectaDbContext(options, new CurrentTenant(accessor));
        return db.Model;
    }

    private static bool HasTenantColumn(IEntityType entity) =>
        entity.FindProperty("ManagerId") is not null || entity.FindProperty("TenantId") is not null;

    // The filter must mention the tenant: AppUser has a filter too (soft delete), which does not separate companies.
    private static bool FiltersByTenant(IEntityType entity) =>
        entity.GetQueryFilter()?.ToString() is { } filter &&
        (filter.Contains("TenantId", StringComparison.Ordinal) || filter.Contains("IsTenantAdmin", StringComparison.Ordinal));

    internal static IReadOnlyList<string> EntitiesWithoutTenantFilter(IModel model, IReadOnlyDictionary<string, string> allowed) =>
        model.GetEntityTypes()
            .Where(HasTenantColumn)
            .Where(entity => !FiltersByTenant(entity))
            .Select(entity => entity.ClrType.Name)
            .Where(name => !allowed.ContainsKey(name))
            .Order()
            .ToArray();

    /// <summary>Allowlist entries that no longer hold: the entity is gone, lost its column, or now has a tenant filter.</summary>
    internal static IReadOnlyList<string> StaleAllowlistEntries(IModel model, IReadOnlyDictionary<string, string> allowed) =>
        allowed.Keys
            .Where(name => !model.GetEntityTypes().Any(entity => entity.ClrType.Name == name && HasTenantColumn(entity) && !FiltersByTenant(entity)))
            .Order()
            .ToArray();

    [Fact]
    public void EveryEntityWithATenantColumnHasATenantQueryFilter()
    {
        var offenders = EntitiesWithoutTenantFilter(Model(), Allowed);

        Assert.True(offenders.Count == 0,
            "These entities carry ManagerId/TenantId but have no query filter that uses it: " + string.Join(", ", offenders) +
            ". Add a filter like the other entities (see InspectaDbContext), or, if it is safe, list it in TenantGuardTests.Allowed with the reason.");
    }

    [Fact]
    public void TheCheckCatchesAnEntityWithoutAFilter()
    {
        var found = EntitiesWithoutTenantFilter(Model(), new Dictionary<string, string>());

        Assert.Equal(["AppUser", "CertificateCounter", "CompanyProfile", "CoordinatorEmailConfig"], found);
    }

    [Fact]
    public void TheAllowlistHasNoStaleEntries()
    {
        var stale = StaleAllowlistEntries(Model(), Allowed);

        Assert.True(stale.Count == 0, "Remove from TenantGuardTests.Allowed: " + string.Join(", ", stale));
    }

    [Fact]
    public void AnAllowlistedEntityThatNowHasATenantFilterIsReportedAsStale()
    {
        var stale = StaleAllowlistEntries(Model(), new Dictionary<string, string> { ["Client"] = "has a filter", ["NoSuchEntity"] = "gone" });

        Assert.Equal(["Client", "NoSuchEntity"], stale);
    }

    public void Dispose() => connection.Dispose();
}
