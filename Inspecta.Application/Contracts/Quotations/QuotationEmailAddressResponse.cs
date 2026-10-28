namespace Inspecta.Application.Contracts.Quotations;

/// <summary>Mailbox address and optional display name from the source email.</summary>
public sealed record QuotationEmailAddressResponse(string Address, string? Name);
