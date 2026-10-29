using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Quotations;

/// <summary>
/// Payload for opening a quotation on a request. Items are copied from the request, prices are filled in
/// afterwards with <c>PUT</c>. Fields sent by older clients (such as the amount) are ignored: the total is
/// always calculated from the item prices.
/// </summary>
public sealed record CreateQuoteRequest
{
    /// <summary>How long the quotation stays valid once sent. Defaults to 14 days.</summary>
    [Range(1, 365)]
    public int? ValidForDays { get; init; }
}
