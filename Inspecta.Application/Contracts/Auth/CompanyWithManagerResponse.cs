using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Auth;

/// <summary>Company state returned by the API.</summary>
public sealed record CompanyResponse(
    Guid Id,
    string Name,
    SubscriptionStatus SubscriptionStatus,
    DateTimeOffset? SubscriptionEndDate,
    DateTimeOffset CreatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt);

/// <summary>Newly provisioned company and its initial technical manager.</summary>
public sealed record CompanyWithManagerResponse(CompanyResponse Company, AuthUserResponse Manager);
