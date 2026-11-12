using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Inspecta.Domain.Tests;

/// <summary>
/// Migration rule: a push to the backend branch migrates the production database at start-up, so a migration must not destroy
/// or reshape data in one step. Dropping or renaming a table or column, making a column required, or shortening one fails here
/// until the migration id is added below on purpose (a reviewer sees it). The way through is two releases: add the new thing and
/// move the data first, remove the old thing in a later migration once nothing reads it.
/// </summary>
public sealed class MigrationSafetyGuardTests
{
    /// <summary>Migration ids that already ran in production and do one of these things. Historic: not an example to follow.</summary>
    private static readonly IReadOnlySet<string> Acknowledged = new HashSet<string>
    {
        // Removed a feature flag column nothing reads any more.
        "20261028102052_RemoveItemHasAngle",
        // Copied every PDF to the new table first, then dropped the old column in the same migration.
        "20261105115329_MoveCertificatePdfsToTheirOwnTable"
    };

    private static readonly Assembly Infrastructure = typeof(Inspecta.Infrastructure.Data.InspectaDbContext).Assembly;

    private static string? RiskOf(MigrationOperation operation) => operation switch
    {
        DropTableOperation drop => $"drops table {drop.Name}",
        DropColumnOperation drop => $"drops column {drop.Table}.{drop.Name}",
        RenameTableOperation rename => $"renames table {rename.Name}",
        RenameColumnOperation rename => $"renames column {rename.Table}.{rename.Name}",
        AlterColumnOperation alter when !alter.IsNullable && alter.OldColumn.IsNullable => $"makes {alter.Table}.{alter.Name} required",
        AlterColumnOperation alter when alter.MaxLength is { } now && alter.OldColumn.MaxLength is { } before && now < before => $"shortens {alter.Table}.{alter.Name}",
        _ => null
    };

    internal static IReadOnlyList<string> DestructiveMigrations(Assembly infrastructure, IReadOnlySet<string> acknowledged) =>
        infrastructure.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(Migration)) && !type.IsAbstract)
            .Select(type => (Id: type.GetCustomAttribute<MigrationAttribute>()!.Id, Migration: (Migration)Activator.CreateInstance(type)!))
            .Where(entry => !acknowledged.Contains(entry.Id))
            .Select(entry => (entry.Id, Risks: entry.Migration.UpOperations.Select(RiskOf).Where(risk => risk is not null).ToArray()))
            .Where(entry => entry.Risks.Length > 0)
            .Select(entry => $"{entry.Id}: {string.Join(", ", entry.Risks)}")
            .Order()
            .ToArray();

    [Fact]
    public void NoMigrationDestroysDataUnlessAcknowledged()
    {
        var offenders = DestructiveMigrations(Infrastructure, Acknowledged);

        Assert.True(offenders.Count == 0,
            "These migrations drop, rename or tighten data: " + string.Join("; ", offenders) +
            ". Split it over two releases (add and move first, remove later), or add the id to MigrationSafetyGuardTests.Acknowledged and say why in the pull request.");
    }

    [Fact]
    public void TheCheckCatchesDestructiveMigrationsWhenNoneAreAcknowledged()
    {
        var found = DestructiveMigrations(Infrastructure, new HashSet<string>());

        Assert.Equal(Acknowledged.Order(), found.Select(entry => entry.Split(':')[0]).Order());
    }

    [Fact]
    public void EveryAcknowledgedMigrationStillExistsAndStillNeedsIt()
    {
        var found = DestructiveMigrations(Infrastructure, new HashSet<string>()).Select(entry => entry.Split(':')[0]).ToHashSet();

        Assert.Empty(Acknowledged.Except(found));
    }
}
