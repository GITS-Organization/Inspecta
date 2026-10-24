using System.ComponentModel.DataAnnotations;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Quotations;

/// <summary>Decision or counteroffer recorded from the client.</summary>
public sealed record QuoteDecisionRequest
{
    public required QuoteStatus Decision { get; init; }

    [MaxLength(2000)]
    public string? Note { get; init; }
}