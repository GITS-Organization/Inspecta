using System.Security.Claims;
using Inspecta.Api.BackgroundServices;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Inspecta.Domain.Tests;

/// <summary>
/// The 30-day clean-up of report files: it runs in the background with no signed-in user, so it works across every company, deletes only the
/// files whose expiry has passed (the bytes go with them) and never lets an error escape.
/// </summary>
public sealed class JobReportCleanupTests : IDisposable
{
    private static readonly byte[] Pdf = "%PDF-1.4 a report"u8.ToArray();

    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;
    private readonly AppUser managerA = AppUser.Create("a@example.com", "Manager A", UserRole.TechnicalManager);
    private readonly AppUser managerB = AppUser.Create("b@example.com", "Manager B", UserRole.TechnicalManager);

    public JobReportCleanupTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        using var db = Admin();
        db.Database.EnsureCreated();
        db.Users.AddRange(managerA, managerB);
        db.SaveChanges();
    }

    // The context as the background service gets it: no HTTP request, so no user and no company.
    private InspectaDbContext Background() => new(options, new CurrentTenant(new HttpContextAccessor()));

    private InspectaDbContext Admin() => new(options, new CurrentTenant(new HttpContextAccessor
    {
        HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "admin")], "test")) }
    }));

    private Guid NewJob(AppUser manager)
    {
        using var db = Admin();
        var request = InspectionRequest.Create("Client", "Cairo", null, [RequestItem.Create("Crane", 1)], manager.Id, managerId: manager.Id);
        db.Requests.Add(request);
        db.SaveChanges();
        return request.Id;
    }

    /// <summary>A file of the job; <paramref name="expiresInDays"/> empty means the job is not delivered yet.</summary>
    private Guid AddFile(AppUser manager, Guid requestId, string name, double? expiresInDays)
    {
        using var db = Admin();
        var file = JobReportFile.Create(manager.Id, requestId, null, manager.Id, manager.FullName, name, "application/pdf", Pdf);
        if (expiresInDays is { } days) file.StartRetention(DateTimeOffset.UtcNow.AddDays(days));
        db.JobReportFiles.Add(file);
        db.SaveChanges();
        return file.Id;
    }

    private (string[] Files, int Bodies) Remaining()
    {
        using var db = Admin();
        return (db.JobReportFiles.Select(file => file.FileName).AsEnumerable().Order().ToArray(), db.Set<JobReportContent>().Count());
    }

    [Fact]
    public async Task CleanupDeletesExpiredReportsOfEveryCompanyAndKeepsTheRest()
    {
        var jobA = NewJob(managerA);
        var jobB = NewJob(managerB);
        AddFile(managerA, jobA, "a-expired.pdf", -1);
        AddFile(managerB, jobB, "b-expired.pdf", -40);
        AddFile(managerA, jobA, "a-still-kept.pdf", 3);
        AddFile(managerB, jobB, "b-not-delivered.pdf", null);

        await using var db = Background();
        var deleted = await new EfJobReportRepository(db).DeleteExpiredAsync(DateTimeOffset.UtcNow, 200);

        Assert.Equal(2, deleted);
        var (files, bodies) = Remaining();
        Assert.Equal(["a-still-kept.pdf", "b-not-delivered.pdf"], files);
        Assert.Equal(2, bodies); // the bytes of the deleted files went with them
    }

    [Fact]
    public async Task CleanupWorksInBatches()
    {
        var job = NewJob(managerA);
        for (var number = 0; number < 5; number++) AddFile(managerA, job, $"old-{number}.pdf", -2);
        AddFile(managerA, job, "kept.pdf", 10);

        await using var db = Background();
        var deleted = await new EfJobReportRepository(db).DeleteExpiredAsync(DateTimeOffset.UtcNow, 2);

        Assert.Equal(5, deleted);
        var (files, bodies) = Remaining();
        Assert.Equal(["kept.pdf"], files);
        Assert.Equal(1, bodies);
    }

    [Fact]
    public async Task NothingExpiredMeansNothingDeleted()
    {
        var job = NewJob(managerA);
        AddFile(managerA, job, "kept.pdf", 10);
        AddFile(managerA, job, "open-job.pdf", null);

        await using var db = Background();

        Assert.Equal(0, await new EfJobReportRepository(db).DeleteExpiredAsync(DateTimeOffset.UtcNow, 200));
        Assert.Equal(2, Remaining().Files.Length);
    }

    [Fact]
    public async Task TheBackgroundServiceCleansWithoutAUserAndAnErrorNeverEscapesIt()
    {
        var job = NewJob(managerA);
        AddFile(managerA, job, "gone.pdf", -1);
        AddFile(managerA, job, "kept.pdf", 5);
        var services = new ServiceCollection().AddScoped<IJobReportRepository>(_ => new EfJobReportRepository(Background())).BuildServiceProvider();

        var service = new JobReportCleanupBackgroundService(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<JobReportCleanupBackgroundService>.Instance);

        Assert.Equal(1, await service.CleanAsync(CancellationToken.None));
        Assert.Equal(["kept.pdf"], Remaining().Files);

        // No repository registered: the pass fails, is logged and returns, so the hourly loop carries on.
        var broken = new JobReportCleanupBackgroundService(
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), NullLogger<JobReportCleanupBackgroundService>.Instance);
        Assert.Equal(0, await broken.CleanAsync(CancellationToken.None));
    }

    public void Dispose() => connection.Dispose();
}
