using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

/// <summary>
/// A company that uses the platform: the unit that owns its clients, requests, certificates and accounts, and the unit whose
/// subscription decides whether anyone in it can work. Every tenant-owned row carries the id of its company (the column is still called
/// <c>ManagerId</c>: it was the id of the company's technical manager before companies existed, and for those companies the company id is
/// that same value, so no row had to change).
/// </summary>
public sealed class Company
{
    public const int MaxNameLength = 200;

    private Company() { }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public SubscriptionStatus SubscriptionStatus { get; private set; } = SubscriptionStatus.Active;
    public DateTimeOffset? SubscriptionEndDate { get; private set; }

    /// <summary>
    /// The technical manager every new request of the company starts with as its reviewer: the first technical manager created in it.
    /// The coordinator can change it per request. Null while the company has no technical manager.
    /// </summary>
    public Guid? DefaultReviewerId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static Company Create(string name, DateTimeOffset? subscriptionEndDate = null, Guid? id = null, DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("The company name is required.", nameof(name));
        if (name.Trim().Length > MaxNameLength) throw new ArgumentException($"The company name can have at most {MaxNameLength} characters.", nameof(name));
        if (id == Guid.Empty) throw new ArgumentException("Company id cannot be empty.", nameof(id));

        return new Company
        {
            Id = id ?? Guid.NewGuid(),
            Name = name.Trim(),
            SubscriptionStatus = SubscriptionStatus.Active,
            SubscriptionEndDate = subscriptionEndDate,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("The company name is required.", nameof(name));
        if (name.Trim().Length > MaxNameLength) throw new ArgumentException($"The company name can have at most {MaxNameLength} characters.", nameof(name));
        Name = name.Trim();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void UpdateSubscription(SubscriptionStatus status, DateTimeOffset? endDate)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        SubscriptionStatus = status;
        SubscriptionEndDate = endDate;
    }

    public void SetDefaultReviewer(Guid? technicalManagerId)
    {
        if (technicalManagerId == Guid.Empty) throw new ArgumentException("Reviewer id cannot be empty.", nameof(technicalManagerId));
        DefaultReviewerId = technicalManagerId;
    }

    /// <summary>The subscription as it stands now: an active one past its end date counts as expired.</summary>
    public SubscriptionStatus GetEffectiveSubscriptionStatus(DateTimeOffset? now = null)
    {
        if (SubscriptionStatus != SubscriptionStatus.Active) return SubscriptionStatus;
        return SubscriptionEndDate is { } endDate && endDate <= (now ?? DateTimeOffset.UtcNow)
            ? SubscriptionStatus.Expired
            : SubscriptionStatus.Active;
    }

    public bool HasActiveSubscription(DateTimeOffset? now = null) => GetEffectiveSubscriptionStatus(now) == SubscriptionStatus.Active;

    /// <summary>Everyone in the company can work: it is switched on and its subscription is valid.</summary>
    public bool IsUsable(DateTimeOffset? now = null) => IsActive && HasActiveSubscription(now);
}
