namespace Inspecta.Application.Contracts.Executive;

/// <summary>The company's targets: what the general manager's numbers are measured against.</summary>
public sealed record ExecutiveTargetsResponse(int CollectionRatePercent, int DaysToCollect, int RequestToDeliveryDays, int ReviewDaysPerStep);

/// <summary>All four targets, replaced together: a percent from 1 to 100 and days from 1 to 365.</summary>
public sealed record SaveExecutiveTargetsRequest(int CollectionRatePercent, int DaysToCollect, int RequestToDeliveryDays, int ReviewDaysPerStep);
