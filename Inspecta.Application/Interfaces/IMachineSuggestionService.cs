using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

/// <summary>Reads an incoming email and finds which of the request client's machines it asks about.</summary>
public interface IMachineSuggestionService
{
    /// <summary>
    /// The client's machines the email mentions (none when the request has no client), without those already on the
    /// request. Nothing is changed.
    /// </summary>
    Task<IReadOnlyList<Services.MachineMatch>> FindAsync(
        InspectionRequest request,
        string? textBody,
        string? htmlBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts the confident matches on a request that has a client and no items yet, as items linked to the machines.
    /// The caller saves them: they are returned (already added to the request) so they can be added explicitly.
    /// A failure to read the email is not an error: it only means nothing is filled.
    /// </summary>
    Task<IReadOnlyList<RequestItem>> AutofillAsync(
        InspectionRequest request,
        string? textBody,
        string? htmlBody,
        CancellationToken cancellationToken = default);
}
