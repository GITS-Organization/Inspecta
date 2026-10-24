using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IQuoteRepository
{
    Task AddAsync(Quote quote, CancellationToken cancellationToken = default);
    Task<Quote?> GetByIdAsync(Guid quoteId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Quote>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}