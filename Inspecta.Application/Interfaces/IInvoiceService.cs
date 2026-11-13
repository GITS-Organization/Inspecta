using Inspecta.Application.Contracts;
using Inspecta.Application.Contracts.Invoices;

namespace Inspecta.Application.Interfaces;

public interface IInvoiceService
{
    Task<PagedList<InvoiceSummary>> ListAsync(Guid actorId, string role, InvoiceListQuery query, CancellationToken cancellationToken = default);
    Task<InvoiceDetail> GetAsync(Guid id, Guid actorId, string role, CancellationToken cancellationToken = default);

    /// <summary>Makes the invoice of a job whose quotation was accepted before invoices existed. Returns the existing one when there already is one.</summary>
    Task<InvoiceDetail> CreateForRequestAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<InvoiceDetail> RecordPaymentAsync(Guid id, RecordPaymentRequest request, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<InvoiceDetail> ReversePaymentAsync(Guid id, Guid paymentId, ReversePaymentRequest request, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<InvoiceDetail> HandOverAsync(Guid id, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<InvoiceDetail> CloseAsync(Guid id, CloseInvoiceRequest? request, Guid actorId, string role, CancellationToken cancellationToken = default);
}
