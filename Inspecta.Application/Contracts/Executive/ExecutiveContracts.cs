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

public enum ClientHealth { Healthy, Watch, AtRisk }

public sealed record RenewalBuckets(int Overdue, int Within7, int Within30, int Within60, int Within90);

public sealed record OverdueByClient(Guid ClientId, string ClientName, int Overdue);

/// <summary><c>DaysUntilDue</c> is negative when overdue.</summary>
public sealed record RenewalItem(
    Guid EquipmentId, string Type, string? Serial, string? Capacity, Guid ClientId, string ClientName,
    Domain.Enums.CertificateResult? LastResult, DateTimeOffset NextDueAt, int DaysUntilDue, bool JobOpened);

/// <summary>
/// Machines due for inspection are the next work. <c>Buckets</c> are cumulative from today (<c>Within30</c> includes <c>Within7</c>); <c>Weekly</c> is the
/// overdue count then the next eleven weeks.
/// </summary>
public sealed record RenewalsResponse(
    RenewalBuckets Buckets, int WithJobCount, int Registered, int SafePercent, IReadOnlyList<int> Weekly,
    IReadOnlyList<OverdueByClient> MostOverdueByClient, int Total, int Page, int PageSize, IReadOnlyList<RenewalItem> Items);

public sealed record ClientCounts(int Active, int WithContract, int Healthy, int Watch, int AtRisk);

public sealed record ClientRow(
    Guid Id, string Name, string? City, string? ReviewerName, bool Contract, int Since, int Machines, int OpenJobs,
    long InvoicedYtdPiastres, long OwedPiastres, long OlderThan60Piastres, int OverdueMachines, int? NextDueInDays, ClientHealth Health);

public sealed record ClientsResponse(ClientCounts Counts, int Total, int Page, int PageSize, IReadOnlyList<ClientRow> Items);

public sealed record ClientContactRow(string Name, string? Role, string? Email, string? Phone);

public sealed record ClientMachineRow(string Type, string? Serial, string? Capacity, Domain.Enums.CertificateResult? LastResult, DateTimeOffset NextDueAt, int DaysUntilDue);

public sealed record ClientRevenue(long InvoicedYtdPiastres, long OwedPiastres, long OlderThan60Piastres);

/// <summary>One client with its contacts, the machines due within 90 days or overdue, its last 20 invoices and its money.</summary>
public sealed record ClientDetailResponse(
    Guid Id, string Name, string? City, string? ReviewerName, bool Contract, int Since, int Machines, int OpenJobs,
    long InvoicedYtdPiastres, long OwedPiastres, long OlderThan60Piastres, int OverdueMachines, int? NextDueInDays, ClientHealth Health,
    IReadOnlyList<ClientContactRow> Contacts, IReadOnlyList<ClientMachineRow> DueMachines, IReadOnlyList<Invoices.InvoiceSummary> Invoices, ClientRevenue Revenue);

public enum StageGroup { Inbox, Field, Review, Delivery }

/// <summary>Jobs in flight: average age and the average days from request to delivery (standard requests delivered in the period) and the ones stuck 30 days or more in the same status.</summary>
public sealed record WorkJobs(int InFlight, int AverageAgeDays, int RequestToDeliveryDays, int StuckOver30);

public sealed record OpenJob(
    string RequestNumber, Guid? ClientId, string ClientName, int Machines, string? ReviewerName,
    Domain.Enums.JobStatus Stage, StageGroup StageGroup, int AgeDays, bool Stuck);

/// <summary><c>Step</c> is request_to_quotation_sent, quotation_to_client_answer, accepted_to_inspector_assigned, inspection_to_submitted, review_to_approved or approved_to_delivered.</summary>
public sealed record StepTime(string Step, double AverageDays, int? TargetDays);

public sealed record QuotationFigures(int Sent, int AcceptedPercent, int Countered, int UnansweredOver14, long UnansweredOver14Piastres, int NewClients);

public sealed record CertificatesMonth(string Month, int Safe, int WithConditions, int NotSafe);

public sealed record ReviewerRow(Guid Id, string Name, int Certificates, int Waiting, double AverageReviewDays, bool Slow);

public sealed record InspectorRow(Guid Id, string Name, int Certificates, int Jobs, DateOnly? OnLeaveUntil);

public sealed record WorkResponse(
    ExecutiveDays Period, ExecutiveDays Previous, WorkJobs Jobs, IReadOnlyList<OpenJob> Open, int Total, int Page, int PageSize,
    IReadOnlyList<StepTime> StepTimes, QuotationFigures Quotations, IReadOnlyList<CertificatesMonth> CertificatesByMonth,
    IReadOnlyList<ReviewerRow> Reviewers, IReadOnlyList<InspectorRow> Inspectors);

/// <summary>Open jobs in four groups, for the pipeline bar.</summary>
public sealed record PipelineGroup(StageGroup Group, int Count);

public sealed record MachinesOverdueKpi(int Count, int DueIn30, bool Met);

public sealed record TodayKpis(CollectedKpi Collected, OutstandingKpi Outstanding, DaysKpi DaysToCollect, DaysKpi RequestToDelivery, MachinesOverdueKpi MachinesOverdue);

public enum DecisionKind { Money, Renewals, Quotation, Work }

public enum DecisionSeverity { Red, Amber, Violet }

public enum ExecutiveScreen { Money, Renewals, Work }

public sealed record DecisionOwner(string Name, string Role);

/// <summary>
/// One thing that needs the general manager. <c>Title</c> is an English sentence for debugging; the app writes its own text from <c>TitleCode</c> and the numbers.
/// <c>Screen</c> and <c>TargetId</c> say where "Open" goes.
/// </summary>
public sealed record Decision(
    DecisionKind Kind, DecisionSeverity Severity, string Title, string TitleCode, Guid? ClientId, string ClientName, DecisionOwner Owner,
    int WaitingDays, long? AmountPiastres, ExecutiveScreen Screen, Guid? TargetId);

/// <summary>Certificates approved in the period and the share of them that are safe to operate (not the machine based figure of Renewals).</summary>
public sealed record CertificatesIssued(int Count, int SafePercent);

public sealed record TodayResponse(
    ExecutiveDays Period, ExecutiveDays Previous, TodayKpis Kpis, IReadOnlyList<Decision> Decisions, IReadOnlyList<MonthlyMoney> Monthly,
    IReadOnlyList<PipelineGroup> Pipeline, CertificatesIssued CertificatesIssued);

public enum ActivityKind { Quote, Job, Cert, Inv, Client }

/// <summary>
/// One thing that happened, as codes and numbers (the app writes the sentence). Several certificates approved or delivered on one request on one day are one
/// row with <c>Count</c>. Nothing from an email is ever in it.
/// </summary>
public sealed record ActivityItem(
    DateTimeOffset At, ActivityKind Kind, string Code, Guid? ClientId, string ClientName, string? RequestNumber, string? ActorName, long? AmountPiastres, int? Count);

/// <summary><c>Kind</c> is inspection or quotationExpiry.</summary>
public sealed record UpcomingItem(DateOnly On, string Kind, string ClientName, string? RequestNumber, string DetailCode, int? Count, string? InspectorName, long? AmountPiastres);

/// <summary><c>NextBefore</c> is passed as <c>before</c> to get the next page; <c>null</c> when there is no more.</summary>
public sealed record ActivityResponse(IReadOnlyList<ActivityItem> Items, DateTimeOffset? NextBefore, IReadOnlyList<UpcomingItem> Upcoming);

public sealed record ReadyToCloseItem(Guid InvoiceId, string Number, string ClientName, long TotalPiastres, int AgeDays);

/// <summary>Invoices handed to finance and paid in full: nothing is left but to close them. <c>Items</c> are the oldest few.</summary>
public sealed record ReadyToClose(int Count, long TotalPiastres, IReadOnlyList<ReadyToCloseItem> Items);

/// <summary>Invoices still with the coordinator (not handed to finance yet) and what they still owe.</summary>
public sealed record NotHandedOver(int Count, long BalancePiastres);

public sealed record OldUnpaidItem(Guid InvoiceId, string Number, string ClientName, long BalancePiastres, int AgeDays);

public sealed record LatestPayment(
    DateTimeOffset At, DateOnly PaidOn, Guid InvoiceId, string InvoiceNumber, string ClientName, long AmountPiastres, string? Label, string? RecordedByName);

/// <summary>The financial manager's Overview: the money figures of the Money screen plus what to do next and what was just received.</summary>
public sealed record FinanceOverviewResponse(
    MoneyResponse Money, ReadyToClose ReadyToClose, NotHandedOver NotHandedOver, IReadOnlyList<OldUnpaidItem> OldUnpaid, IReadOnlyList<LatestPayment> LatestPayments);
