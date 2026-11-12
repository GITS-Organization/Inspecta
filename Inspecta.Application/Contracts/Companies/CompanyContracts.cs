using System.ComponentModel.DataAnnotations;
using Inspecta.Application.Contracts.Auth;
using Inspecta.Application.Contracts.Company;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Companies;

/// <summary>A company: whether it is on, its subscription, and who reviews the certificates of its new requests by default.</summary>
/// <param name="SubscriptionStatus">As it stands now: an active subscription past its end date reads as expired.</param>
public sealed record CompanyResponse(
    Guid Id,
    string Name,
    bool IsActive,
    SubscriptionStatus SubscriptionStatus,
    DateTimeOffset? SubscriptionEndDate,
    Guid? DefaultReviewerId,
    string? DefaultReviewerName,
    DateTimeOffset CreatedAt);

/// <summary>A new company together with the account it is handed over with.</summary>
public sealed record CreatedCompanyResponse(CompanyResponse Company, AuthUserResponse Admin);

/// <summary>Creates a company and its first account, the company admin.</summary>
public sealed record CreateCompanyRequest
{
    [Required, MaxLength(200)]
    public required string Name { get; init; }

    [Required, EmailAddress, MaxLength(320)]
    public required string AdminEmail { get; init; }

    [Required, MaxLength(200)]
    public required string AdminFullName { get; init; }

    [Required, MinLength(12), MaxLength(256)]
    public required string AdminPassword { get; init; }

    [Range(1, 120)]
    public int SubscriptionMonths { get; init; } = 12;

    /// <summary>The company's identity (logo, accreditations, colours, contacts), saved with it; optional.</summary>
    public SaveCompanyProfileRequest? CompanyProfile { get; init; }
}

public sealed record RenameCompanyRequest([Required, MaxLength(200)] string Name);

public sealed record SetCompanyStatusRequest(bool IsActive);

public sealed record UpdateCompanySubscriptionRequest
{
    public required SubscriptionStatus SubscriptionStatus { get; init; }
    public DateTimeOffset? SubscriptionEndDate { get; init; }
}

public sealed record SetDefaultReviewerRequest(Guid ReviewerId);
