using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>
/// The data step of the migration that introduced companies, run on SQLite exactly as written for PostgreSQL: every technical manager
/// becomes a company with the same id, so nothing keyed by it has to move, and nobody ends up without the company they worked for.
/// </summary>
public sealed class CompanyBackfillTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;

    public CompanyBackfillTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        using var db = Context();
        db.Database.EnsureCreated();
    }

    public void Dispose() => connection.Dispose();

    private InspectaDbContext Context() => new(options, new CurrentTenant(new HttpContextAccessor
    {
        HttpContext = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("role", "admin")], "test", "name", "role")) }
    }));

    private void Run()
    {
        using var db = Context();
        db.Database.ExecuteSqlRaw(CompanyBackfill.InsertCompanies);
        db.Database.ExecuteSqlRaw(CompanyBackfill.AttachManagersToTheirCompany);
        db.Database.ExecuteSqlRaw(CompanyBackfill.SetReviewerOfExistingRequests);
    }

    [Fact]
    public async Task EachTechnicalManagerBecomesACompanyWithTheSameIdAndJoinsIt()
    {
        var named = AppUser.Create("named@acme.com", "Named Manager", UserRole.TechnicalManager, subscriptionEndDate: DateTimeOffset.UtcNow.AddMonths(6));
        var unnamed = AppUser.Create("unnamed@beta.com", "Beta Manager", UserRole.TechnicalManager);
        var coordinator = AppUser.Create("c@acme.com", "Coordinator", UserRole.Coordinator, managerId: named.Id);
        var owner = AppUser.Create("owner@gits.com", "Owner", UserRole.Admin);
        await using (var seed = Context())
        {
            seed.Users.AddRange(named, unnamed, coordinator, owner);
            var profile = CompanyProfile.Create(named.Id);
            profile.Update("Acme Inspection Co.", null, null, null, "Cairo", [], [], "", null, "#184896", "#34A046");
            seed.CompanyProfiles.Add(profile);
            await seed.SaveChangesAsync();
        }

        Run();

        await using var check = Context();
        var companies = await check.Companies.AsNoTracking().ToListAsync();
        Assert.Equal(2, companies.Count);
        var acme = companies.Single(company => company.Id == named.Id);
        Assert.Equal("Acme Inspection Co.", acme.Name);
        Assert.True(acme.IsActive);
        Assert.Equal(SubscriptionStatus.Active, acme.SubscriptionStatus);
        Assert.NotNull(acme.SubscriptionEndDate);
        Assert.Equal(named.Id, acme.DefaultReviewerId);
        Assert.Equal("Beta Manager", companies.Single(company => company.Id == unnamed.Id).Name);

        var users = await check.Users.AsNoTracking().ToDictionaryAsync(user => user.Id);
        Assert.Equal(named.Id, users[named.Id].ManagerId);
        Assert.Equal(unnamed.Id, users[unnamed.Id].ManagerId);
        Assert.Equal(named.Id, users[coordinator.Id].ManagerId);
        Assert.Null(users[owner.Id].ManagerId);
    }

    [Fact]
    public async Task ASwitchedOffOrDeletedManagerBecomesASwitchedOffCompanyAndALapsedOneStaysLapsed()
    {
        var off = AppUser.Create("off@acme.com", "Off", UserRole.TechnicalManager, isActive: false);
        var gone = AppUser.Create("gone@acme.com", "Gone", UserRole.TechnicalManager);
        gone.SoftDelete();
        var lapsed = AppUser.Create("lapsed@acme.com", "Lapsed", UserRole.TechnicalManager);
        lapsed.UpdateSubscription(SubscriptionStatus.Suspended, null);
        await using (var seed = Context())
        {
            seed.Users.AddRange(off, gone, lapsed);
            await seed.SaveChangesAsync();
        }

        Run();

        await using var check = Context();
        var companies = await check.Companies.AsNoTracking().ToDictionaryAsync(company => company.Id);
        Assert.False(companies[off.Id].IsActive);
        Assert.Null(companies[off.Id].DefaultReviewerId);
        Assert.Null(companies[gone.Id].DefaultReviewerId);
        Assert.False(companies[gone.Id].IsActive);
        Assert.True(companies[lapsed.Id].IsActive);
        Assert.Equal(SubscriptionStatus.Suspended, companies[lapsed.Id].SubscriptionStatus);
        Assert.False(companies[lapsed.Id].IsUsable());
    }

    [Fact]
    public async Task RunningItAgainChangesNothing()
    {
        var manager = AppUser.Create("m@acme.com", "Manager", UserRole.TechnicalManager);
        await using (var seed = Context())
        {
            seed.Users.Add(manager);
            await seed.SaveChangesAsync();
        }

        Run();
        await using (var rename = Context())
        {
            var company = await rename.Companies.SingleAsync();
            company.Rename("Renamed by an admin");
            await rename.SaveChangesAsync();
        }
        Run();

        await using var check = Context();
        var only = await check.Companies.AsNoTracking().SingleAsync();
        Assert.Equal("Renamed by an admin", only.Name);
    }

    [Fact]
    public async Task EveryExistingRequestStartsWithItsCompanysDefaultReviewer()
    {
        var manager = AppUser.Create("m@acme.com", "Manager", UserRole.TechnicalManager);
        var coordinator = AppUser.Create("c@acme.com", "Coordinator", UserRole.Coordinator, managerId: manager.Id);
        var request = InspectionRequest.Create("Acme", "Cairo", null, [RequestItem.Create("Crane", 1)], coordinator.Id, managerId: manager.Id);
        var already = InspectionRequest.Create("Acme 2", "Cairo", null, [RequestItem.Create("Crane", 1)], coordinator.Id, managerId: manager.Id);
        var chosen = Guid.NewGuid();
        already.UseReviewer(chosen);
        await using (var seed = Context())
        {
            seed.Users.AddRange(manager, coordinator);
            seed.Requests.AddRange(request, already);
            await seed.SaveChangesAsync();
        }

        Run();

        await using var check = Context();
        var requests = await check.Requests.AsNoTracking().ToDictionaryAsync(item => item.Id);
        Assert.Equal(manager.Id, requests[request.Id].ReviewerId);
        Assert.Equal(chosen, requests[already.Id].ReviewerId);
    }

    [Fact]
    public async Task AFreshDatabaseWithNoManagersGetsNoCompanies()
    {
        Run();

        await using var check = Context();
        Assert.Empty(await check.Companies.ToListAsync());
    }
}
