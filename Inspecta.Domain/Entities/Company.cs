using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

public sealed class Company
{
    private Company() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public SubscriptionStatus SubscriptionStatus { get; private set; } = SubscriptionStatus.Active;
    public DateTimeOffset? SubscriptionEndDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public static Company Create(
        string name,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? subscriptionEndDate = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Company name is required.", nameof(name));

        return new Company
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            SubscriptionEndDate = subscriptionEndDate
        };
    }

    public void UpdateSubscription(SubscriptionStatus status, DateTimeOffset? endDate)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status));

        SubscriptionStatus = status;
        SubscriptionEndDate = endDate;
    }

    public SubscriptionStatus GetEffectiveSubscriptionStatus(DateTimeOffset? now = null)
    {
        if (IsDeleted || SubscriptionStatus == SubscriptionStatus.Expired)
            return SubscriptionStatus.Expired;
        if (SubscriptionStatus == SubscriptionStatus.Suspended)
            return SubscriptionStatus.Suspended;
        return SubscriptionEndDate is { } endDate && endDate <= (now ?? DateTimeOffset.UtcNow)
            ? SubscriptionStatus.Expired
            : SubscriptionStatus.Active;
    }

    public bool HasActiveSubscription(DateTimeOffset? now = null) =>
        !IsDeleted && GetEffectiveSubscriptionStatus(now) == SubscriptionStatus.Active;

    public void SoftDelete(DateTimeOffset? deletedAt = null)
    {
        if (IsDeleted)
            return;

        IsDeleted = true;
        DeletedAt = deletedAt ?? DateTimeOffset.UtcNow;
    }
}
