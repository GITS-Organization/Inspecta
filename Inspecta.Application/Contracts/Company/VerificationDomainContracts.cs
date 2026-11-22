namespace Inspecta.Application.Contracts.Company;

/// <summary>The address to make the company's primary verification address, for example <c>verify.yourcompany.com</c> (no scheme, path or port).</summary>
public sealed record SetVerificationDomainRequest(string? Host);

/// <summary>One verification address of a company. <c>isPrimary</c> is the one new certificates use; the others keep old QR codes working.</summary>
public sealed record VerificationDomainResponse(Guid Id, string Host, bool IsPrimary, DateTimeOffset CreatedAt);
