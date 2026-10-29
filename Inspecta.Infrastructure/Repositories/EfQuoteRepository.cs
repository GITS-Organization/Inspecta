using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
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

    public async Task<IReadOnlyList<Quote>> ListAsync(QuoteListFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Quotes.AsNoTracking().Include(quote => quote.Request)
            .Where(quote => quote.Request != null);
        if (filter.CoordinatorId is Guid coordinatorId)
            query = query.Where(quote =>
                quote.Request!.Source != RequestSource.Email || quote.Request.CoordinatorId == coordinatorId);
        if (filter.ManagerId is Guid managerId) query = query.Where(quote => quote.Request!.ManagerId == managerId);
        if (filter.CurrentOnly) query = query.Where(quote => quote.SupersededAt == null);
        if (filter.Open is true) query = query.Where(quote => quote.Status == QuoteStatus.Open);
        if (filter.Open is false) query = query.Where(quote => quote.Status != QuoteStatus.Open);
        if (filter.Sent is true) query = query.Where(quote => quote.SentAt != null);
        if (filter.Sent is false) query = query.Where(quote => quote.SentAt == null);
        if (filter.Accepted is true) query = query.Where(quote => quote.Status == QuoteStatus.Accepted);
        if (filter.Accepted is false) query = query.Where(quote => quote.Status == QuoteStatus.Rejected);
        if (filter.Search is { Length: > 0 } search)
        {
            var escaped = search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = "%" + escaped + "%";
            query = query.Where(quote =>
                (quote.ClientName != null && EF.Functions.ILike(quote.ClientName, pattern)) ||
                (quote.QuotationNumber != null && EF.Functions.ILike(quote.QuotationNumber, pattern)) ||
                EF.Functions.ILike(quote.Request!.Location, pattern));
        }

        return await query
            .OrderByDescending(quote => quote.UpdatedAt)
            .ThenBy(quote => quote.Id)
            .Skip(filter.Skip)
            .Take(filter.Take)
            .ToListAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}