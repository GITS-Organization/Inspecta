using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Requests;

/// <summary>
/// Saves the request screen in one step: client, location, preferred date, access notes and, optionally, the
/// whole equipment list. Everything is saved together or nothing is.
/// </summary>
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

    /// <summary>
    /// The complete equipment list as it should be after saving. Leave it out (<c>null</c>) to keep the items as they are.
    /// Items with an <c>id</c> of this request are updated, items without an <c>id</c> are added, and existing items that
    /// are not listed are removed. An empty list removes every item.
    /// </summary>
    public IReadOnlyList<SaveRequestItem>? Items { get; init; }

    /// <summary>
    /// The <c>updatedAt</c> of the request as the screen last read it. When set and the request changed since, nothing is
    /// saved and the API answers <c>409</c>, so a stale screen cannot overwrite or delete newer changes.
    /// </summary>
    public DateTimeOffset? ExpectedUpdatedAt { get; init; }

    /// <summary>
    /// Also open the quotation in the same save: its items are copied from the request (prices empty), the request moves to
    /// <c>quote_draft</c>, and the response carries the quotation. If a draft already exists it is refreshed from the items and
    /// keeps its prices. The request must be complete (client name, location and at least one item), otherwise nothing is saved.
    /// </summary>
    public bool PrepareQuotation { get; init; }
}

/// <summary>One equipment item of the whole-request save.</summary>
public sealed record SaveRequestItem
{
    /// <summary>The id of an existing item of this request; leave empty for a new item.</summary>
    public Guid? Id { get; init; }

    [Required, MaxLength(200)]
    public required string Name { get; init; }

    [Range(1, 10000)]
    public int Quantity { get; init; } = 1;

    public Guid? EquipmentId { get; init; }
    public string? Type { get; init; }
    public string? Capacity { get; init; }
    public string? SerialNumber { get; init; }

    /// <summary>Where the equipment sits on site, when different from the client location.</summary>
    [MaxLength(500)]
    public string? Location { get; init; }

    [MaxLength(4000)]
    public string? IssueDescription { get; init; }
}
