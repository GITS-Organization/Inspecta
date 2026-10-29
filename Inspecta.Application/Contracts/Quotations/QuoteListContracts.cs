using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Quotations;

/// <summary>Where a quotation stands, as the quotation list groups them.</summary>
public enum QuoteStage
{
    /// <summary>Open and not sent yet: the ones the coordinator is still working on.</summary>
    Draft,

    /// <summary>Sent to the client and waiting for an answer.</summary>
    Sent,
    Accepted,
    Rejected
}

/// <summary>Filters and paging for the quotation list.</summary>
/// <param name="ManagerId">Admin only: narrow the global view to one team.</param>
/// <param name="Stage">Only quotations in this stage.</param>
/// <param name="IncludeSuperseded">Also return old revisions that a counter offer replaced. Off by default.</param>
public sealed record QuoteListQuery(
    Guid? ManagerId = null,
    QuoteStage? Stage = null,
    string? Search = null,
    bool IncludeSuperseded = false,
    int Page = 1,
    int PageSize = QuoteListQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;
}

/// <summary>One row of the quotation list; open the full quotation with <c>GET /requests/{requestId}/quotes/{id}</c>.</summary>
/// <remarks><c>canEdit</c> is true for a draft the current user may price and send.</remarks>
public sealed record QuoteSummaryResponse(
    Guid Id,
    Guid RequestId,
    int Version,
    QuoteStage Stage,
    QuoteStatus Status,
    JobStatus RequestStatus,
    string? QuotationNumber,
    string ProposalNumber,
    string? ClientName,
    string Location,
    long AmountPiastres,
    int TotalUnits,
    int MissingPrices,
    DateTimeOffset ValidUntil,
    bool IsExpired,
    bool IsCurrent,
    bool CanEdit,
    DateTimeOffset? SentAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
