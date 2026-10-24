using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class QuoteService(IQuoteRepository quoteRepository, IRequestRepository requestRepository) : IQuoteService
{
    public async Task<IReadOnlyList<QuoteResponse>> ListAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        (await quoteRepository.ListForRequestAsync(requestId, cancellationToken)).Select(Map).ToArray();

    public async Task<QuoteResponse> CreateAsync(Guid requestId, Guid actorId, CreateQuoteRequest request, CancellationToken cancellationToken = default)
    {
        var inspectionRequest = await requestRepository.GetByIdAsync(requestId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Request was not found.");
        if (inspectionRequest.Status is not (JobStatus.RequestReceived or JobStatus.ClientCountered))
            throw new InvalidOperationException("A quote can only be prepared for a new request or a client counteroffer.");
        if (inspectionRequest.ClientId is null) throw new InvalidOperationException("Match a client before preparing a quote.");

        inspectionRequest.TransitionTo(JobStatus.QuoteDraft, actorId);
        var existingQuotes = await quoteRepository.ListForRequestAsync(requestId, cancellationToken);
        var quote = Quote.Create(requestId, existingQuotes.Count + 1, request.AmountPiastres, request.ValidUntil, actorId);
        await quoteRepository.AddAsync(quote, cancellationToken);
        return Map(quote);
    }

    public async Task<QuoteResponse> SendAsync(Guid requestId, Guid quoteId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var quote = await quoteRepository.GetByIdAsync(quoteId, cancellationToken)
            ?? throw new KeyNotFoundException("Quote was not found.");
        if (quote.RequestId != requestId) throw new KeyNotFoundException("Quote was not found for this request.");
        var request = await requestRepository.GetByIdAsync(quote.RequestId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Request was not found.");
        quote.Send();
        request.TransitionTo(JobStatus.QuoteSent, actorId);
        await quoteRepository.SaveAsync(cancellationToken);
        return Map(quote);
    }

    public async Task<QuoteResponse> RecordDecisionAsync(Guid requestId, Guid quoteId, Guid actorId, QuoteDecisionRequest decision, CancellationToken cancellationToken = default)
    {
        var quote = await quoteRepository.GetByIdAsync(quoteId, cancellationToken)
            ?? throw new KeyNotFoundException("Quote was not found.");
        if (quote.RequestId != requestId) throw new KeyNotFoundException("Quote was not found for this request.");
        var request = await requestRepository.GetByIdAsync(quote.RequestId, tracking: true, cancellationToken)
            ?? throw new KeyNotFoundException("Request was not found.");
        quote.RecordClientDecision(decision.Decision, decision.Note);
        var status = decision.Decision switch
        {
            QuoteStatus.Countered => JobStatus.ClientCountered,
            QuoteStatus.Accepted => JobStatus.QuoteAccepted,
            QuoteStatus.Rejected => JobStatus.QuoteRejected,
            _ => throw new ArgumentException("Invalid quote decision.", nameof(decision))
        };
        request.TransitionTo(status, actorId, decision.Note);
        await quoteRepository.SaveAsync(cancellationToken);
        return Map(quote);
    }

    private static QuoteResponse Map(Quote quote) => new(
        quote.Id, quote.RequestId, quote.Version, quote.AmountPiastres, quote.ValidUntil, quote.Status, quote.ClientReply, quote.CreatedAt);
}