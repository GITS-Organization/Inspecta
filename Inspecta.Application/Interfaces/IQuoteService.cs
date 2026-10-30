using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Pdf;

namespace Inspecta.Application.Interfaces;

public interface IQuoteService
{
    Task<IReadOnlyList<QuoteResponse>> ListAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuoteSummaryResponse>> ListVisibleAsync(Guid actorId, string role, QuoteListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuoteSummaryResponse>> ListQueueAsync(Guid actorId, string role, QuotationQueue? queue, QuotationQueueQuery query, CancellationToken cancellationToken = default);
    Task<QuoteResponse> GetAsync(Guid requestId, Guid quoteId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<QuoteResponse> CreateAsync(Guid requestId, Guid actorId, string role, CreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<QuoteResponse> UpdateAsync(Guid requestId, Guid quoteId, Guid actorId, string role, UpdateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<PdfFile> GetPdfAsync(Guid requestId, Guid quoteId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<Contracts.Requests.RequestResponse> SaveRequestAndPrepareQuotationAsync(Guid requestId, Guid actorId, string role, Contracts.Requests.UpdateRequestDetailsRequest details, CancellationToken cancellationToken = default);
    Task<Contracts.Requests.RequestResponse> RejectRequestAsync(Guid requestId, Guid actorId, string role, Contracts.Requests.RejectRequestRequest request, CancellationToken cancellationToken = default);
    Task<QuoteResponse> AcceptAsync(Guid requestId, Guid quoteId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<QuoteResponse> SendAsync(Guid requestId, Guid quoteId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<QuoteResponse> RecordDecisionAsync(Guid requestId, Guid quoteId, Guid actorId, string role, QuoteDecisionRequest request, CancellationToken cancellationToken = default);
}
