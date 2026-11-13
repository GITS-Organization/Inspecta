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

    /// <summary>The money received in a period (default: the current month, Cairo). The platform owner may narrow to one company.</summary>
    Task<RevenueResponse> RevenueAsync(DateOnly? from, DateOnly? to, Guid? managerId, string role, CancellationToken cancellationToken = default);

    /// <summary>The invoice as a PDF, from its frozen numbers, with what is paid and what is left as of today.</summary>
    Task<Inspecta.Application.Pdf.PdfFile> GetPdfAsync(Guid id, Guid actorId, string role, CancellationToken cancellationToken = default);

    /// <summary>The email that would carry the invoice (the same one <see cref="SendEmailAsync"/> sends), with the coordinator's edits applied. Sends nothing.</summary>
    Task<Contracts.Quotations.QuoteEmailPreviewResponse> PreviewEmailAsync(Guid id, Contracts.Quotations.SendQuoteEmailRequest? email, Guid actorId, string role, CancellationToken cancellationToken = default);

    /// <summary>Emails the invoice PDF to the client. Only after the certificates were delivered; can be repeated. A failed send changes nothing.</summary>
    Task<InvoiceDetail> SendEmailAsync(Guid id, Contracts.Quotations.SendQuoteEmailRequest? email, Guid actorId, string role, CancellationToken cancellationToken = default);
}
