using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Auth;

/// <summary>Admin changes to a technical manager's subscription state and end date.</summary>
public sealed record UpdateTechnicalManagerSubscriptionRequest
{
    public required SubscriptionStatus SubscriptionStatus { get; init; }

    public DateTimeOffset? SubscriptionEndDate { get; init; }
}
