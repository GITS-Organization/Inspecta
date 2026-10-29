using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Quotations;

/// <summary>A quotation revision with its priced items and current state.</summary>
/// <remarks>
/// <c>status</c> is <c>open</c>, <c>accepted</c> or <c>rejected</c>. An open quotation is still being prepared
/// while <c>sentAt</c> is empty, and waits for the client once it is set. <c>missingPrices</c> lists the items
/// that block sending. <c>supersededAt</c> is set on a sent revision that a newer one (a counter offer) replaced.
/// <c>emails</c> shows the PDF emails sent for it and whether they were delivered.
/// </remarks>
public sealed record QuoteResponse(
    Guid Id,
    Guid RequestId,
    int Version,
    long AmountPiastres,
    DateTimeOffset ValidUntil,
    QuoteStatus Status,
    string? ClientReply,
    DateTimeOffset CreatedAt,
    string ProposalNumber = "",
    string? ClientName = null,
    int ValidForDays = 14,
    string? Message = null,
    IReadOnlyList<QuoteItemResponse>? Items = null,
    IReadOnlyList<string>? ScopeOfWork = null,
    IReadOnlyList<string>? Notes = null,
    IReadOnlyList<Guid>? MissingPrices = null,
    int TotalUnits = 0,
    bool CanEdit = false,
    bool IsExpired = false,
    DateTimeOffset? SentAt = null,
    DateTimeOffset UpdatedAt = default,
    DateTimeOffset? SupersededAt = null,
    IReadOnlyList<QuoteEmailResponse>? Emails = null);

/// <summary>One priced line of a quotation.</summary>
public sealed record QuoteItemResponse(
    Guid? RequestItemId,
    string Description,
    string Type,
    string? PrNumber,
    int Quantity,
    long? UnitPricePiastres,
    string PricingUnit,
    long TotalPiastres);

/// <summary>An email sent for a quotation, with its delivery state.</summary>
/// <param name="Kind"><c>offer</c> or <c>accept</c>.</param>
/// <param name="Status"><c>pending</c>, <c>sent</c> or <c>failed</c>.</param>
public sealed record QuoteEmailResponse(
    OutboundEmailKind Kind,
    OutboundEmailStatus Status,
    string ToAddress,
    int Attempts,
    DateTimeOffset? SentAt,
    string? LastError);
