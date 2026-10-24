namespace Inspecta.Application.Contracts.Clients;

/// <summary>Client profile with archived state and contacts.</summary>
public sealed record ClientResponse(Guid Id, string Name, string? Notes, DateTimeOffset CreatedAt, DateTimeOffset? ArchivedAt, IReadOnlyList<ClientContactResponse> Contacts);

/// <summary>Client contact details.</summary>
public sealed record ClientContactResponse(Guid Id, string Name, string? Email, string? Phone, bool IsPrimary);