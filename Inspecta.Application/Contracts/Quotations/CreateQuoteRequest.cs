using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Quotations;

/// <summary>Payload for preparing a quote revision.</summary>
public sealed record CreateQuoteRequest
{
    [Range(0, long.MaxValue)]
    public long AmountPiastres { get; init; }

    public required DateTimeOffset ValidUntil { get; init; }
}