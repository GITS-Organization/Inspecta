using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

/// <summary>Which quotations a list call may return, beyond the tenant filter applied by the data layer.</summary>
/// <param name="CoordinatorId">
/// Coordinator scope: quotations of manual requests stay visible to the whole team, those of email requests only to
/// the coordinator that received them.
/// </param>
/// <param name="ManagerId">Only quotations of this team (used by Admin to narrow the global view).</param>
/// <param name="Open">True for open quotations, false for closed ones (accepted or rejected).</param>
/// <param name="Sent">Whether the quotation was sent to the client.</param>
/// <param name="Accepted">True for accepted, false for rejected, among closed quotations.</param>
/// <param name="CurrentOnly">Leave out revisions that a newer one replaced.</param>
public sealed record QuoteListFilter(
    Guid? CoordinatorId = null,
    Guid? ManagerId = null,
    bool? Open = null,
    bool? Sent = null,
    bool? Accepted = null,
    bool CurrentOnly = true,
    string? Search = null,
    int Skip = 0,
    int Take = int.MaxValue);

public interface IQuoteRepository
{
    Task AddAsync(Quote quote, CancellationToken cancellationToken = default);
    Task<Quote?> GetByIdAsync(Guid quoteId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Quote>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Quotations across requests, newest activity first, each with its request loaded.</summary>
    Task<IReadOnlyList<Quote>> ListAsync(QuoteListFilter filter, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
