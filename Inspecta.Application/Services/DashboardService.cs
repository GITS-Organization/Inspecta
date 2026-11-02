using Inspecta.Application.Contracts.Dashboard;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class DashboardService(
    IDashboardRepository dashboardRepository,
    IUserRepository userRepository,
    TimeProvider? clock = null) : IDashboardService
{
    private const int DueSoonDays = 30;
    private const int DefaultWorkloadDays = 30;
    private const int DefaultChartMonths = 7;
    private const int MaxDays = 731;
    private const int DueItemsShown = 10;

    public async Task<DashboardOverviewResponse> GetOverviewAsync(
        Guid? managerId, Guid? clientId, Guid? inspectorId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var period = ResolvePeriod(today, from, to);
        var filter = new DashboardFilter(managerId, clientId, inspectorId);

        // One read of the approved certificates serves the chart, the two month counters and the workload.
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var previousMonth = thisMonth.AddMonths(-1);
        var readFrom = new[] { period.ChartFrom, period.From, previousMonth }.Min();
        var readTo = new[] { period.To, today }.Max();
        var approved = await dashboardRepository.ListApprovedCertificatesAsync(filter, StartOf(readFrom), EndOf(readTo), cancellationToken);

        var pipeline = await PipelineAsync(filter, cancellationToken);
        var due = await dashboardRepository.ListEquipmentDueAsync(filter, EndOf(today.AddDays(DueSoonDays)), cancellationToken);
        var inspectors = await userRepository.ListAsync(managerId, UserRole.Inspector, isActive: true, cancellationToken);

        return new DashboardOverviewResponse(
            period,
            new DashboardAttention(
                due.Count(row => DateOf(row.DueAt) < today),
                due.Count(row => DateOf(row.DueAt) >= today),
                pipeline.FirstOrDefault(item => item.Status == JobStatus.CertificateSubmitted)?.Count ?? 0),
            pipeline,
            Certificates(approved, period, thisMonth, previousMonth),
            EquipmentDue(due, today),
            Workload(approved, inspectors, inspectorId, period));
    }

    private static DashboardPeriod ResolvePeriod(DateOnly today, DateOnly? from, DateOnly? to)
    {
        if (from is null != to is null)
            throw new ArgumentException("Give both from and to, or neither.");
        if (from is { } start && to is { } end)
        {
            if (start > end) throw new ArgumentException("from cannot be after to.");
            if (end.DayNumber - start.DayNumber > MaxDays) throw new ArgumentException("The period cannot be longer than two years.");
            return new DashboardPeriod(start, end, start);
        }

        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-(DefaultChartMonths - 1));
        return new DashboardPeriod(today.AddDays(-(DefaultWorkloadDays - 1)), today, firstMonth);
    }

    // Every status is listed, in the order of the workflow, so the screen can draw its tiles without gaps.
    private async Task<IReadOnlyList<RequestStatusCount>> PipelineAsync(DashboardFilter filter, CancellationToken cancellationToken)
    {
        var counts = (await dashboardRepository.CountRequestsByStatusAsync(filter, cancellationToken))
            .ToDictionary(item => item.Status, item => item.Count);
        return Enum.GetValues<JobStatus>().Select(status => new RequestStatusCount(status, counts.GetValueOrDefault(status))).ToArray();
    }

    private static DashboardCertificates Certificates(
        IReadOnlyList<ApprovedCertificateRow> approved, DashboardPeriod period, DateOnly thisMonth, DateOnly previousMonth)
    {
        var inChart = approved.Where(row => DateOf(row.ApprovedAt) >= period.ChartFrom && DateOf(row.ApprovedAt) <= period.To).ToArray();
        var months = new List<DashboardMonth>();
        for (var month = new DateOnly(period.ChartFrom.Year, period.ChartFrom.Month, 1);
             month <= new DateOnly(period.To.Year, period.To.Month, 1);
             month = month.AddMonths(1))
        {
            var rows = inChart.Where(row => DateOf(row.ApprovedAt).Year == month.Year && DateOf(row.ApprovedAt).Month == month.Month).ToArray();
            months.Add(new DashboardMonth(
                $"{month.Year:D4}-{month.Month:D2}",
                rows.Count(row => row.Result == CertificateResult.SafeToOperate),
                rows.Count(row => row.Result == CertificateResult.SafeWithConditions),
                rows.Count(row => row.Result == CertificateResult.NotSafe),
                rows.Length));
        }

        int InMonth(DateOnly month) =>
            approved.Count(row => DateOf(row.ApprovedAt).Year == month.Year && DateOf(row.ApprovedAt).Month == month.Month);
        return new DashboardCertificates(InMonth(thisMonth), InMonth(previousMonth), months);
    }

    private static DashboardEquipmentDue EquipmentDue(IReadOnlyList<DueEquipmentRow> due, DateOnly today)
    {
        var items = due
            .OrderBy(row => row.DueAt)
            .Take(DueItemsShown)
            .Select(row =>
            {
                var days = DateOf(row.DueAt).DayNumber - today.DayNumber;
                return new DashboardDueItem(row.Id, row.Name, row.SerialNumber, row.ClientId, row.ClientName, row.DueAt, days, days < 0);
            })
            .ToArray();
        return new DashboardEquipmentDue(due.Count, due.Count(row => DateOf(row.DueAt) < today), items);
    }

    private static IReadOnlyList<DashboardInspectorWorkload> Workload(
        IReadOnlyList<ApprovedCertificateRow> approved, IReadOnlyList<AppUser> inspectors, Guid? inspectorId, DashboardPeriod period)
    {
        var counts = approved
            .Where(row => DateOf(row.ApprovedAt) >= period.From && DateOf(row.ApprovedAt) <= period.To)
            .GroupBy(row => row.InspectorId)
            .ToDictionary(group => group.Key, group => group.Count());
        return inspectors
            .Where(inspector => inspectorId is null || inspector.Id == inspectorId)
            .Select(inspector => new DashboardInspectorWorkload(inspector.Id, inspector.FullName, counts.GetValueOrDefault(inspector.Id)))
            .OrderByDescending(item => item.Certificates)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static DateOnly DateOf(DateTimeOffset moment) => DateOnly.FromDateTime(moment.UtcDateTime);

    private static DateTimeOffset StartOf(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static DateTimeOffset EndOf(DateOnly day) => new(day.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
}
