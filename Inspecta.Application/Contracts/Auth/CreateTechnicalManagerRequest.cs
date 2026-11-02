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

    /// <summary>
    /// The company identity of the team (name, address, contacts, logo, accreditations, colours), set in the same call. It is checked before
    /// the account is created, so a mistake in it creates nothing. Left out, the team uses the built-in identity until
    /// <c>PUT /admin/managers/{managerId}/company-profile</c> sets one. A profile without its own logo or accreditations gets the built-in ones.
    /// </summary>
    public Company.SaveCompanyProfileRequest? CompanyProfile { get; init; }
}
