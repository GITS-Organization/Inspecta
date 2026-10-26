using System.ComponentModel.DataAnnotations;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Auth;

/// <summary>Admin changes to a company's subscription state and end date.</summary>
public sealed record UpdateCompanySubscriptionRequest
{
    public required SubscriptionStatus SubscriptionStatus { get; init; }

    public DateTimeOffset? SubscriptionEndDate { get; init; }
}
