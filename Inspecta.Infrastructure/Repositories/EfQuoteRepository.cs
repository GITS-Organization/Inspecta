using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfQuoteRepository(InspectaDbContext dbContext) : IQuoteRepository
{
    public async Task AddAsync(Quote quote, CancellationToken cancellationToken = default)
    {
        dbContext.Quotes.Add(quote);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Quote?> GetByIdAsync(Guid quoteId, CancellationToken cancellationToken = default) =>
        dbContext.Quotes.SingleOrDefaultAsync(quote => quote.Id == quoteId, cancellationToken);

    public async Task<IReadOnlyList<Quote>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        await dbContext.Quotes.AsNoTracking().Where(quote => quote.RequestId == requestId)
            .OrderBy(quote => quote.Version).ToListAsync(cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}