using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Auth;

/// <summary>Payload for creating a technical manager account with its initial subscription.</summary>
public sealed record CreateTechnicalManagerRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }

    [Required, MaxLength(200)]
    public required string FullName { get; init; }

    [Required, MinLength(12), MaxLength(256)]
    public required string Password { get; init; }

    /// <summary>Subscription duration in months.</summary>
    [Range(1, 120)]
    public int SubscriptionMonths { get; init; } = 1;
}
