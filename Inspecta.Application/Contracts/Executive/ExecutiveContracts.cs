namespace Inspecta.Application.Contracts.Executive;

/// <summary>The company's targets: what the general manager's numbers are measured against.</summary>
public sealed record ExecutiveTargetsResponse(int CollectionRatePercent, int DaysToCollect, int RequestToDeliveryDays, int ReviewDaysPerStep);

/// <summary>All four targets, replaced together: a percent from 1 to 100 and days from 1 to 365.</summary>
public sealed record SaveExecutiveTargetsRequest(int CollectionRatePercent, int DaysToCollect, int RequestToDeliveryDays, int ReviewDaysPerStep);

/// <summary>The days an answer covers (both days included).</summary>
public sealed record ExecutiveDays(DateOnly From, DateOnly To);

/// <summary>Money collected in the period against what was invoiced, with the target. <c>Met</c> is rate at or above target.</summary>
public sealed record CollectedKpi(long CollectedPiastres, long InvoicedPiastres, int RatePercent, int PreviousRatePercent, int TargetPercent, bool Met);

/// <summary>What is still owed on invoices that are not closed, and the part older than 60 days. Invoices closed by hand stand apart in <c>ClosedByHandPiastres</c>.</summary>
public sealed record OutstandingKpi(long TotalPiastres, long OlderThan60Piastres, int OlderThan60Count, long ClosedByHandPiastres);

/// <summary>An average in days against its target. <c>Met</c> is days at or below target.</summary>
public sealed record DaysKpi(int Days, int PreviousDays, int TargetDays, bool Met);

public sealed record InvoicedKpi(long TotalPiastres, long PreviousTotalPiastres);

public sealed record MoneyKpis(InvoicedKpi Invoiced, CollectedKpi Collected, OutstandingKpi Outstanding, DaysKpi DaysToCollect);

/// <summary><c>Bucket</c> is 0_30, 31_60, 61_90 or over_90: the age of the invoice in days.</summary>
public sealed record AgingBucket(string Bucket, long AmountPiastres, int Count);

public sealed record OwedByClient(Guid? ClientId, string ClientName, long OwedPiastres, long OlderThan60Piastres);

/// <summary><c>Month</c> is yyyy-MM.</summary>
public sealed record MonthlyMoney(string Month, long InvoicedPiastres, long CollectedPiastres);

public sealed record MoneyResponse(
    ExecutiveDays Period,
    ExecutiveDays Previous,
    MoneyKpis Kpis,
    IReadOnlyList<AgingBucket> Aging,
    IReadOnlyList<OwedByClient> WhoOwesMost,
    IReadOnlyList<MonthlyMoney> Monthly);

/// <summary>What every executive call is asked: the days, the company (already resolved for the caller) and an optional reviewer.</summary>
public sealed record ExecutiveQuery(DateOnly? From, DateOnly? To, Guid? ManagerId, Guid? ReviewerId);
