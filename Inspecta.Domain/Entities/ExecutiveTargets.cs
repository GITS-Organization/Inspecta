namespace Inspecta.Domain.Entities;

/// <summary>
/// What a company aims for, so the general manager's numbers can say "on target" or "off target": the share of invoiced money collected,
/// the days to collect it, the days from request to delivery, and the days a review should take. One row per company; a company that never
/// set them uses the defaults.
/// </summary>
public sealed class ExecutiveTargets
{
    public const int DefaultCollectionRatePercent = 85;
    public const int DefaultDaysToCollect = 30;
    public const int DefaultRequestToDeliveryDays = 28;
    public const int DefaultReviewDaysPerStep = 2;

    private ExecutiveTargets() { }

    /// <summary>The company (tenant); also the key, so a company has at most one row.</summary>
    public Guid ManagerId { get; private set; }
    public int CollectionRatePercent { get; private set; }
    public int DaysToCollect { get; private set; }
    public int RequestToDeliveryDays { get; private set; }
    public int ReviewDaysPerStep { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }

    public static ExecutiveTargets Create(Guid managerId, int collectionRatePercent, int daysToCollect, int requestToDeliveryDays, int reviewDaysPerStep, Guid actorId)
    {
        if (managerId == Guid.Empty) throw new ArgumentException("Company is required.", nameof(managerId));
        var targets = new ExecutiveTargets { ManagerId = managerId };
        targets.Update(collectionRatePercent, daysToCollect, requestToDeliveryDays, reviewDaysPerStep, actorId);
        return targets;
    }

    /// <summary>Replaces all four targets: a percent from 1 to 100, days from 1 to 365.</summary>
    public void Update(int collectionRatePercent, int daysToCollect, int requestToDeliveryDays, int reviewDaysPerStep, Guid actorId)
    {
        if (collectionRatePercent is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(collectionRatePercent), "The collection rate must be from 1 to 100 percent.");
        if (daysToCollect is < 1 or > 365) throw new ArgumentOutOfRangeException(nameof(daysToCollect), "Days to collect must be from 1 to 365.");
        if (requestToDeliveryDays is < 1 or > 365) throw new ArgumentOutOfRangeException(nameof(requestToDeliveryDays), "Request to delivery must be from 1 to 365 days.");
        if (reviewDaysPerStep is < 1 or > 365) throw new ArgumentOutOfRangeException(nameof(reviewDaysPerStep), "Review time must be from 1 to 365 days.");

        CollectionRatePercent = collectionRatePercent;
        DaysToCollect = daysToCollect;
        RequestToDeliveryDays = requestToDeliveryDays;
        ReviewDaysPerStep = reviewDaysPerStep;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = actorId;
    }
}
