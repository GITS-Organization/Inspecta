using System.Security.Claims;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>
/// The manager's overview. The request counts and the users come from a real database; the certificates and the items due are given as rows
/// (SQLite cannot compare dates with offsets: the queries behind those rows are checked for PostgreSQL in <see cref="PostgresQueryTranslationTests"/>).
/// </summary>
public sealed class DashboardTests : IDisposable
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record Certificate(Guid Team, Guid Client, ApprovedCertificateRow Row);

    private sealed record Equipment(Guid Team, Guid Client, DueEquipmentRow Row);

    /// <summary>Counts the requests in the database, and answers the certificates and the items from the rows it was given, with the same filters.</summary>
    private sealed class Repository(InspectaDbContext db) : IDashboardRepository
    {
        public List<Certificate> Certificates { get; } = [];
        public List<Equipment> Items { get; } = [];

        public Task<IReadOnlyList<RequestStatusCount>> CountRequestsByStatusAsync(DashboardFilter filter, CancellationToken cancellationToken = default) =>
            new EfDashboardRepository(db).CountRequestsByStatusAsync(filter, cancellationToken);

        public Task<IReadOnlyList<ApprovedCertificateRow>> ListApprovedCertificatesAsync(
            DashboardFilter filter, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ApprovedCertificateRow>>(Certificates
                .Where(item => (filter.ManagerId is null || item.Team == filter.ManagerId) &&
                               (filter.ClientId is null || item.Client == filter.ClientId) &&
                               (filter.InspectorId is null || item.Row.InspectorId == filter.InspectorId) &&
                               item.Row.ApprovedAt >= from && item.Row.ApprovedAt <= to)
                .Select(item => item.Row)
                .ToArray());

        public Task<IReadOnlyList<DueEquipmentRow>> ListEquipmentDueAsync(
            DashboardFilter filter, DateTimeOffset cutoff, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DueEquipmentRow>>(Items
                .Where(item => (filter.ManagerId is null || item.Team == filter.ManagerId) &&
                               (filter.ClientId is null || item.Client == filter.ClientId) &&
                               item.Row.DueAt <= cutoff)
                .Select(item => item.Row)
                .ToArray());
    }

    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;

    // A fixed day in the middle of a month, so nothing here depends on when the test runs.
    private readonly DateTimeOffset now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly AppUser manager = AppUser.Create("manager@acme.com", "Manager", UserRole.TechnicalManager);
    private readonly AppUser otherManager = AppUser.Create("other@acme.com", "Other Manager", UserRole.TechnicalManager);
    private readonly AppUser coordinator = AppUser.Create("coordinator@acme.com", "Coordinator", UserRole.Coordinator);
    private readonly AppUser ragab;
    private readonly AppUser mohamed;
    private readonly Client alNoor;
    private readonly Client delta;
    private readonly Client foreign;

    public DashboardTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        ragab = AppUser.Create("ragab@acme.com", "Ragab Nasr", UserRole.Inspector, managerId: manager.Id);
        mohamed = AppUser.Create("mohamed@acme.com", "Mohamed Ali", UserRole.Inspector, managerId: manager.Id);
        alNoor = Client.Create("Al Noor", managerId: manager.Id);
        delta = Client.Create("Delta Steel", managerId: manager.Id);
        foreign = Client.Create("Foreign Co", managerId: otherManager.Id);

        using var db = Context();
        db.Database.EnsureCreated();
        db.AddRange(manager, otherManager, coordinator, ragab, mohamed, alNoor, delta, foreign);
        db.SaveChanges();
    }

    private InspectaDbContext Context() => new(options, new CurrentTenant(new HttpContextAccessor
    {
        HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "admin")], "test")) }
    }));

    private static void Set(object target, string property, object? value) =>
        target.GetType().GetProperty(property)!.SetValue(target, value);

    private InspectionRequest Request(Client client, Guid managerId, JobStatus? status = null, Guid? inspectorId = null)
    {
        var request = InspectionRequest.Create(
            client.Name, "Site", client.Id, [RequestItem.Create("Crane", 1)], coordinator.Id, managerId: managerId);
        if (status is { } value) Set(request, nameof(InspectionRequest.Status), value);
        if (inspectorId is { } inspector) Set(request, nameof(InspectionRequest.InspectorId), inspector);
        return request;
    }

    private async Task<Repository> SeedAsync(InspectaDbContext db)
    {
        var maybe = InspectionRequest.CreateFromEmail(
            manager.Id, coordinator.Id,
            new IncomingEmail("1", 1, "Hello", null, "x@y.com", "X", "Hi", [], [], [], [], [], "<1@x>", null, null, now),
            triage: RequestTriage.Maybe);
        db.Requests.AddRange(
            Request(alNoor, manager.Id),
            Request(delta, manager.Id),
            Request(alNoor, manager.Id, JobStatus.CertificateSubmitted, ragab.Id),
            Request(delta, manager.Id, JobStatus.InProgress, mohamed.Id),
            Request(alNoor, manager.Id, JobStatus.SentToClient, ragab.Id),
            Request(foreign, otherManager.Id, JobStatus.CertificateSubmitted),
            maybe);
        await db.SaveChangesAsync();

        var repository = new Repository(db);
        repository.Certificates.AddRange([
            new Certificate(manager.Id, alNoor.Id, new ApprovedCertificateRow(ragab.Id, now.AddHours(-2), CertificateResult.SafeToOperate)),
            new Certificate(manager.Id, alNoor.Id, new ApprovedCertificateRow(ragab.Id, now.AddHours(-3), CertificateResult.SafeToOperate)),
            new Certificate(manager.Id, alNoor.Id, new ApprovedCertificateRow(ragab.Id, now.AddHours(-4), CertificateResult.NotSafe)),
            new Certificate(manager.Id, alNoor.Id, new ApprovedCertificateRow(ragab.Id, now.AddHours(-5), null)),
            // Mohamed's one is 40 days old: in last month's count and in the chart, outside the 30 days of the workload.
            new Certificate(manager.Id, alNoor.Id, new ApprovedCertificateRow(mohamed.Id, now.AddDays(-40), CertificateResult.SafeWithConditions)),
            new Certificate(otherManager.Id, foreign.Id, new ApprovedCertificateRow(ragab.Id, now.AddHours(-1), CertificateResult.SafeToOperate))]);
        repository.Items.AddRange([
            new Equipment(manager.Id, alNoor.Id, new DueEquipmentRow(Guid.NewGuid(), "Soon forklift", "FK-1", alNoor.Id, "Al Noor", now.AddDays(10))),
            new Equipment(manager.Id, delta.Id, new DueEquipmentRow(Guid.NewGuid(), "Overdue crane", null, delta.Id, "Delta Steel", now.AddDays(-3))),
            new Equipment(otherManager.Id, foreign.Id, new DueEquipmentRow(Guid.NewGuid(), "Other team crane", null, foreign.Id, "Foreign Co", now.AddDays(-5)))]);
        return repository;
    }

    private DashboardService Service(InspectaDbContext db, IDashboardRepository repository) =>
        new(repository, new EfUserRepository(db), new FixedClock(now));

    [Fact]
    public async Task TheOverviewCountsOnlyTheManagersTeamAndTheRealRequests()
    {
        await using var db = Context();
        var service = Service(db, await SeedAsync(db));

        var overview = await service.GetOverviewAsync(manager.Id, null, null, null, null);

        // Every status is listed; maybe emails and the other team's requests are not counted.
        Assert.Equal(Enum.GetValues<JobStatus>().Length, overview.Pipeline.Count);
        int Count(JobStatus status) => overview.Pipeline.Single(item => item.Status == status).Count;
        Assert.Equal(2, Count(JobStatus.RequestReceived));
        Assert.Equal(1, Count(JobStatus.CertificateSubmitted));
        Assert.Equal(1, Count(JobStatus.InProgress));
        Assert.Equal(1, Count(JobStatus.SentToClient));
        Assert.Equal(0, Count(JobStatus.Assigned));

        Assert.Equal(1, overview.Attention.EquipmentOverdue);
        Assert.Equal(1, overview.Attention.EquipmentDueSoon);
        Assert.Equal(1, overview.Attention.JobsAwaitingReview);
        Assert.Equal(3, overview.Attention.Total);

        // Overdue first.
        Assert.Equal(2, overview.EquipmentDue.Total);
        Assert.Equal(1, overview.EquipmentDue.Overdue);
        Assert.Equal(["Overdue crane", "Soon forklift"], overview.EquipmentDue.Items.Select(item => item.Name));
        Assert.True(overview.EquipmentDue.Items[0].Overdue);
        Assert.Equal(-3, overview.EquipmentDue.Items[0].DaysUntilDue);
        Assert.Equal(10, overview.EquipmentDue.Items[1].DaysUntilDue);
        Assert.False(overview.EquipmentDue.Items[1].Overdue);
        Assert.Equal("Delta Steel", overview.EquipmentDue.Items[0].ClientName);
    }

    [Fact]
    public async Task TheCertificatesAreCountedByMonthAndVerdictAndTheWorkloadPerInspector()
    {
        await using var db = Context();
        var service = Service(db, await SeedAsync(db));

        var overview = await service.GetOverviewAsync(manager.Id, null, null, null, null);

        // The last seven months, the current one last; every month is listed.
        Assert.Equal(["2026-05", "2026-06", "2026-07", "2026-08", "2026-09", "2026-10", "2026-11"], overview.Certificates.ByMonth.Select(month => month.Month));
        Assert.Equal(new DateOnly(2026, 5, 1), overview.Period.ChartFrom);
        Assert.Equal(4, overview.Certificates.ThisMonth);
        Assert.Equal(1, overview.Certificates.PreviousMonth);

        var november = overview.Certificates.ByMonth[^1];
        Assert.Equal((2, 0, 1, 4), (november.Safe, november.WithConditions, november.NotSafe, november.Total));
        var october = overview.Certificates.ByMonth[^2];
        Assert.Equal((0, 1, 0, 1), (october.Safe, october.WithConditions, october.NotSafe, october.Total));

        // Both inspectors are listed, the busier one first; the 40-day-old certificate is outside the 30 days.
        Assert.Equal(new DateOnly(2026, 10, 17), overview.Period.From);
        Assert.Equal(["Ragab Nasr", "Mohamed Ali"], overview.InspectorWorkload.Select(item => item.Name));
        Assert.Equal([4, 0], overview.InspectorWorkload.Select(item => item.Certificates));
    }

    [Fact]
    public async Task TheClientAndInspectorFiltersNarrowTheFigures()
    {
        await using var db = Context();
        var service = Service(db, await SeedAsync(db));

        var onlyDelta = await service.GetOverviewAsync(manager.Id, delta.Id, null, null, null);
        Assert.Equal(1, onlyDelta.EquipmentDue.Total);
        Assert.Equal(0, onlyDelta.Attention.JobsAwaitingReview);
        Assert.Equal(1, onlyDelta.Pipeline.Single(item => item.Status == JobStatus.InProgress).Count);
        Assert.Equal(0, onlyDelta.Certificates.ByMonth.Sum(month => month.Total));

        var onlyMohamed = await service.GetOverviewAsync(manager.Id, null, mohamed.Id, null, null);
        Assert.Equal([mohamed.FullName], onlyMohamed.InspectorWorkload.Select(item => item.Name));
        Assert.Equal(1, onlyMohamed.Certificates.ByMonth.Sum(month => month.Total));
        Assert.Equal(1, onlyMohamed.Pipeline.Single(item => item.Status == JobStatus.InProgress).Count);
        Assert.Equal(0, onlyMohamed.Pipeline.Single(item => item.Status == JobStatus.RequestReceived).Count);
        // The items due have no inspector, so that filter leaves them as they are.
        Assert.Equal(2, onlyMohamed.EquipmentDue.Total);
    }

    [Fact]
    public async Task APeriodGivenByTheCallerSetsTheWorkloadAndTheChart()
    {
        await using var db = Context();
        var service = Service(db, await SeedAsync(db));
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var wide = await service.GetOverviewAsync(manager.Id, null, null, today.AddDays(-60), today);
        Assert.Equal(today.AddDays(-60), wide.Period.From);
        Assert.Equal([4, 1], wide.InspectorWorkload.Select(item => item.Certificates));
        Assert.Equal(["2026-09", "2026-10", "2026-11"], wide.Certificates.ByMonth.Select(month => month.Month));

        var old = await service.GetOverviewAsync(manager.Id, null, null, today.AddDays(-400), today.AddDays(-300));
        Assert.All(old.InspectorWorkload, item => Assert.Equal(0, item.Certificates));
        Assert.All(old.Certificates.ByMonth, month => Assert.Equal(0, month.Total));
        // This month and last month are the real ones, whatever period is asked.
        Assert.Equal(4, old.Certificates.ThisMonth);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetOverviewAsync(manager.Id, null, null, today, null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetOverviewAsync(manager.Id, null, null, null, today));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetOverviewAsync(manager.Id, null, null, today, today.AddDays(-1)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetOverviewAsync(manager.Id, null, null, today.AddYears(-3), today));
    }

    [Fact]
    public async Task AnAdminWithoutATeamSeesEveryTeam()
    {
        await using var db = Context();
        var service = Service(db, await SeedAsync(db));

        var all = await service.GetOverviewAsync(null, null, null, null, null);

        Assert.Equal(2, all.Attention.JobsAwaitingReview);
        Assert.Equal(3, all.EquipmentDue.Total);
        Assert.Equal(6, all.Certificates.ByMonth.Sum(month => month.Total));
    }

    public void Dispose() => connection.Dispose();
}
