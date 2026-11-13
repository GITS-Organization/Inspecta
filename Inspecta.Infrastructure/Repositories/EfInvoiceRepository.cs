using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfInvoiceRepository(InspectaDbContext dbContext) : IInvoiceRepository
{
    public Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        dbContext.Invoices.Add(invoice);
        return Task.CompletedTask;
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, bool tracking = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Invoices.Include(invoice => invoice.Payments).Include(invoice => invoice.Events).AsSplitQuery();
        if (!tracking) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(invoice => invoice.Id == id, cancellationToken);
    }

    public Task<Invoice?> GetByQuoteIdAsync(Guid quoteId, CancellationToken cancellationToken = default) =>
        dbContext.Invoices.AsNoTracking().SingleOrDefaultAsync(invoice => invoice.QuoteId == quoteId, cancellationToken);

    // Invoices are few (one per accepted quotation), so the page is cut in memory: that lets the payment state and the text search
    // work the same on every database and avoids ordering by a DateTimeOffset in SQL.
    public async Task<InvoicePage> ListAsync(InvoiceListFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Invoices.AsNoTracking().Include(invoice => invoice.Payments).AsSplitQuery().AsQueryable();
        if (filter.ManagerId is Guid managerId) query = query.Where(invoice => invoice.ManagerId == managerId);
        if (filter.RequestId is Guid requestId) query = query.Where(invoice => invoice.RequestId == requestId);
        if (filter.Stage is InvoiceStage stage) query = query.Where(invoice => invoice.Stage == stage);
        if (filter.ReviewerId is Guid reviewerId)
            query = query.Where(invoice => dbContext.Requests.Any(request => request.Id == invoice.RequestId && request.ReviewerId == reviewerId));
        if (filter.CoordinatorId is Guid coordinatorId)
            query = query.Where(invoice => dbContext.Requests.Any(request =>
                request.Id == invoice.RequestId && (request.Source != RequestSource.Email || request.CoordinatorId == coordinatorId)));

        var rows = await query.ToListAsync(cancellationToken);
        IEnumerable<Invoice> matching = rows;
        if (filter.From is DateOnly from) matching = matching.Where(invoice => InvoiceCalendar.Of(invoice.CreatedAt) >= from);
        if (filter.To is DateOnly to) matching = matching.Where(invoice => InvoiceCalendar.Of(invoice.CreatedAt) <= to);
        if (!string.IsNullOrWhiteSpace(filter.PaymentState))
            matching = matching.Where(invoice => invoice.PaymentState == filter.PaymentState);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var text = filter.Search.Trim();
            matching = matching.Where(invoice =>
                invoice.ClientName.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                invoice.Number.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                (invoice.QuotationReference?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var ordered = matching.OrderByDescending(invoice => invoice.CreatedAt).ThenBy(invoice => invoice.Id).ToList();
        return new InvoicePage(ordered.Skip(filter.Skip).Take(filter.Take).ToList(), ordered.Count);
    }

    public async Task<IReadOnlyList<RevenueRow>> RevenueAsync(
        DateOnly from, DateOnly to, Guid? managerId, CancellationToken cancellationToken = default) =>
        await dbContext.InvoicePayments.AsNoTracking()
            .Where(payment => payment.PaidOn >= from && payment.PaidOn <= to && (managerId == null || payment.ManagerId == managerId))
            .Join(dbContext.Invoices, payment => payment.InvoiceId, invoice => invoice.Id,
                (payment, invoice) => new RevenueRow(invoice.ClientId, invoice.ClientName, payment.PaidOn, payment.AmountPiastres))
            .ToListAsync(cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);

}
