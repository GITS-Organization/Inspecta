using Inspecta.Application.Contracts.Requests;

namespace Inspecta.Application.Contracts.Dashboard;

/// <summary>Everything the overview screen of the technical manager shows, in one answer.</summary>
public sealed record DashboardOverviewResponse(
    DashboardPeriod Period,
    DashboardAttention Attention,
    IReadOnlyList<RequestStatusCount> Pipeline,
    DashboardCertificates Certificates,
    DashboardEquipmentDue EquipmentDue,
    IReadOnlyList<DashboardInspectorWorkload> InspectorWorkload);

/// <summary>The dates the figures were counted over.</summary>
/// <param name="From">First day of the inspector workload.</param>
/// <param name="To">Last day of the inspector workload and of the certificates chart.</param>
/// <param name="ChartFrom">First day of the certificates chart (the first of a month unless the caller gave <c>from</c>).</param>
public sealed record DashboardPeriod(DateOnly From, DateOnly To, DateOnly ChartFrom);

/// <summary>What needs the manager's attention now.</summary>
/// <param name="EquipmentOverdue">Items whose next inspection date has passed.</param>
/// <param name="EquipmentDueSoon">Items whose next inspection is due in the next 30 days.</param>
/// <param name="JobsAwaitingReview">Jobs whose certificates are submitted and wait for the manager (<c>certificate_submitted</c>).</param>
public sealed record DashboardAttention(int EquipmentOverdue, int EquipmentDueSoon, int JobsAwaitingReview)
{
    public int Total => EquipmentOverdue + EquipmentDueSoon + JobsAwaitingReview;
}

/// <summary>Approved (issued) certificates.</summary>
/// <param name="ThisMonth">Issued in the current calendar month.</param>
/// <param name="PreviousMonth">Issued in the month before.</param>
public sealed record DashboardCertificates(int ThisMonth, int PreviousMonth, IReadOnlyList<DashboardMonth> ByMonth);

/// <summary>Certificates issued in one month, by the verdict they carry.</summary>
/// <param name="Month"><c>yyyy-MM</c>.</param>
/// <param name="Safe"><c>safe_to_operate</c>.</param>
/// <param name="WithConditions"><c>safe_with_conditions</c>.</param>
/// <param name="NotSafe"><c>not_safe</c>.</param>
/// <param name="Total">All issued that month (a certificate without a verdict counts here only).</param>
public sealed record DashboardMonth(string Month, int Safe, int WithConditions, int NotSafe, int Total);

/// <summary>Items overdue or due in the next 30 days.</summary>
/// <param name="Total">All of them, overdue ones included.</param>
/// <param name="Items">The first ones: overdue first, then by due date.</param>
public sealed record DashboardEquipmentDue(int Total, int Overdue, IReadOnlyList<DashboardDueItem> Items);

/// <param name="DaysUntilDue">Negative when overdue.</param>
public sealed record DashboardDueItem(
    Guid Id,
    string Name,
    string? SerialNumber,
    Guid ClientId,
    string ClientName,
    DateTimeOffset DueAt,
    int DaysUntilDue,
    bool Overdue);

/// <summary>How many certificates an inspector had approved in the period (every active inspector is listed, even with 0).</summary>
public sealed record DashboardInspectorWorkload(Guid InspectorId, string Name, int Certificates);
