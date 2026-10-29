using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Quotations;

/// <summary>Saves the draft of an open, unsent quotation.</summary>
public sealed record UpdateQuoteRequest
{
    /// <summary>One entry for every item of the request, each exactly once. A price may stay empty in a draft.</summary>
    [Required]
    public required IReadOnlyList<UpdateQuoteItem> Items { get; init; }

    [Range(1, 365)]
    public int? ValidForDays { get; init; }

    /// <summary>What the visit covers, in order.</summary>
    public IReadOnlyList<string>? ScopeOfWork { get; init; }

    /// <summary>Notes printed on the proposal, in order.</summary>
    public IReadOnlyList<string>? Notes { get; init; }

    [MaxLength(4000)]
    public string? Message { get; init; }

    /// <summary>
    /// Reference printed on the document and in the email, for example <c>INT/417/2026 rev01</c>. Leave empty to keep
    /// the current one (generated as <c>QT-year-code revNN</c>).
    /// </summary>
    [MaxLength(64)]
    public string? ProposalNumber { get; init; }

    /// <summary>Person the offer is addressed to (the "Attention" line). Defaults to the sender name of the original email.</summary>
    [MaxLength(200)]
    public string? ContactName { get; init; }
}

/// <summary>The price of one request item.</summary>
public sealed record UpdateQuoteItem
{
    public required Guid RequestItemId { get; init; }

    /// <summary>Fixed price per visit in piastres (1 EGP = 100 piastres).</summary>
    [Range(0, long.MaxValue)]
    public long? UnitPricePiastres { get; init; }
}
