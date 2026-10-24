using Inspecta.Application.Contracts.Quotations;

namespace Inspecta.Application.Interfaces;

public interface IQuoteService
{
    Task<IReadOnlyList<QuoteResponse>> ListAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<QuoteResponse> CreateAsync(Guid requestId, Guid actorId, CreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<QuoteResponse> SendAsync(Guid requestId, Guid quoteId, Guid actorId, CancellationToken cancellationToken = default);
    Task<QuoteResponse> RecordDecisionAsync(Guid requestId, Guid quoteId, Guid actorId, QuoteDecisionRequest request, CancellationToken cancellationToken = default);
}