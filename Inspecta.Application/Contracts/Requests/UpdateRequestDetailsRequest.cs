using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Requests;

/// <summary>Replaces the editable details of a request that has not yet been quoted.</summary>
public sealed record UpdateRequestDetailsRequest
{
    /// <summary>Optional existing client record to link; must belong to the request's team.</summary>
    public Guid? ClientId { get; init; }

    [MaxLength(200)]
    public string? ClientName { get; init; }

    [MaxLength(500)]
    public string? Location { get; init; }

    public DateTimeOffset? PreferredDate { get; init; }

    [MaxLength(4000)]
    public string? AccessNotes { get; init; }
}
