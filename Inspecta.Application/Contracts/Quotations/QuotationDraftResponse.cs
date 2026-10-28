using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Quotations;

/// <summary>A received client email parsed into a quotation draft.</summary>
public sealed record QuotationDraftResponse(
    Guid Id,
    Guid CoordinatorId,
    string? ClientName,
    string? Location,
    string? MachineDetails,
    string RawEmailBody,
    string? SenderEmail,
    string? EmailSubject,
    QuotationDraftStatus Status,
    DateTimeOffset CreatedAt);
