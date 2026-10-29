using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Interfaces;

/// <summary>Delivery state of an outbound email, without its body and attachment.</summary>
public sealed record OutboundEmailSummary(
    Guid QuoteId,
    OutboundEmailKind Kind,
    OutboundEmailStatus Status,
    string ToAddress,
    int Attempts,
    DateTimeOffset? SentAt,
    string? LastError,
    DateTimeOffset CreatedAt);

public interface IOutboundEmailRepository
{
    Task<OutboundEmail?> GetAsync(Guid quoteId, OutboundEmailKind kind, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboundEmailSummary>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Saves a new email. Throws <see cref="InvalidOperationException"/> when another one exists for the same quote and kind.</summary>
    Task AddAsync(OutboundEmail email, CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}
