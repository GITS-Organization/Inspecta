namespace Inspecta.Application.Contracts.Auth;

public sealed record CompanyResponse(Guid Id, string Name, bool IsActive, DateTimeOffset CreatedAt);

public sealed record CompanyWithManagerResponse(CompanyResponse Company, AuthUserResponse Manager);
