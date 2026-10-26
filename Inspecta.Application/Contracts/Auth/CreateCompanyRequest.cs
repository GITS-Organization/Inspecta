using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Auth;

/// <summary>Payload for provisioning a company with its initial technical manager.</summary>
public sealed record CreateCompanyRequest
{
    [Required, MaxLength(200)]
    public required string Name { get; init; }

    [Required, EmailAddress, MaxLength(320)]
    public required string ManagerEmail { get; init; }

    [Required, MaxLength(200)]
    public required string ManagerFullName { get; init; }

    [Required, MinLength(12), MaxLength(256)]
    public required string ManagerPassword { get; init; }

    /// <summary>Subscription duration in months. Defaults to one month when omitted.</summary>
    [Range(1, 120)]
    public int SubscriptionMonths { get; init; } = 1;
}
