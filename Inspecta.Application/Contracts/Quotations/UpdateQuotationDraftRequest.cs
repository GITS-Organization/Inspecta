using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Quotations;

/// <summary>Coordinator-edited fields for an incoming quotation draft.</summary>
public sealed record UpdateQuotationDraftRequest
{
    [MaxLength(200)]
    public string? ClientName { get; init; }

    [MaxLength(500)]
    public string? Location { get; init; }

    [MaxLength(4000)]
    public string? MachineDetails { get; init; }

    public long? AmountPiastres { get; init; }

    public DateTimeOffset? ValidUntil { get; init; }
}
