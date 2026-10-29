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
/// <param name="InspectorId">Only quotations whose request is assigned to this inspector.</param>
/// <param name="Assigned"><c>true</c> for quotations already given to an inspector, <c>false</c> for those still waiting for one.</param>
/// <param name="IncludeSuperseded">Also return old revisions that a counter offer replaced. Off by default.</param>
public sealed record QuoteListQuery(
    Guid? ManagerId = null,
    QuoteStage? Stage = null,
    string? Search = null,
    bool IncludeSuperseded = false,
    int Page = 1,
    int PageSize = QuoteListQuery.DefaultPageSize,
    Guid? InspectorId = null,
    bool? Assigned = null)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;
}

/// <summary>One row of the quotation list; open the full quotation with <c>GET /requests/{requestId}/quotes/{id}</c>.</summary>
/// <remarks>
/// <c>canEdit</c> is true for a draft the current user may price and send. <c>inspectorId</c>, <c>inspectorName</c> and
/// <c>scheduledAt</c> are set once the accepted job was given to an inspector. An inspector only gets their own accepted
/// quotations and never sees prices: <c>amountPiastres</c> is empty for them.
/// </remarks>
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
    long? AmountPiastres,
    int TotalUnits,
    int MissingPrices,
    DateTimeOffset ValidUntil,
    bool IsExpired,
    bool IsCurrent,
    bool CanEdit,
    DateTimeOffset? SentAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? InspectorId = null,
    string? InspectorName = null,
    DateTimeOffset? ScheduledAt = null);
