using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Interfaces;

/// <summary>Which invoices a list call may return, beyond the company filter applied by the data layer.</summary>
/// <param name="CoordinatorId">
/// Coordinator scope: invoices of manual requests stay visible to the whole team, those of email requests only to the coordinator that received them.
/// </param>
/// <param name="ManagerId">Only invoices of this company (used by the platform owner to narrow the global view).</param>
/// <param name="PaymentState"><c>unpaid</c>, <c>partial</c> or <c>paid</c>.</param>
/// <param name="From">Only invoices made on or after this day (Cairo).</param>
/// <param name="To">Only invoices made on or before this day (Cairo).</param>
public sealed record InvoiceListFilter(
    InvoiceStage? Stage = null,
    string? PaymentState = null,
    string? Search = null,
    Guid? RequestId = null,
    Guid? CoordinatorId = null,
    Guid? ManagerId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Skip = 0,
    int Take = 50);

/// <summary>One page of invoices, newest first, with how many matched in all.</summary>
public sealed record InvoicePage(IReadOnlyList<Invoice> Items, int Total);

/// <summary>One payment or reversal with the client it was for, the unit revenue is summed from.</summary>
public sealed record RevenueRow(Guid? ClientId, string ClientName, DateOnly PaidOn, long AmountPiastres);

public interface IInvoiceRepository
{
    Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);

    /// <summary>The invoice with its payments and history, or <c>null</c> when it does not exist or belongs to another company.</summary>
    Task<Invoice?> GetByIdAsync(Guid id, bool tracking = false, CancellationToken cancellationToken = default);

    Task<Invoice?> GetByQuoteIdAsync(Guid quoteId, CancellationToken cancellationToken = default);

    /// <summary>The invoices that match, newest first, with their payments (so each knows what was paid).</summary>
    Task<InvoicePage> ListAsync(InvoiceListFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Every payment and reversal received from <paramref name="from"/> to <paramref name="to"/>, both days included.</summary>
    Task<IReadOnlyList<RevenueRow>> RevenueAsync(DateOnly from, DateOnly to, Guid? managerId, CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}
