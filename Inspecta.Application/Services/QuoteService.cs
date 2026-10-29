using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class QuoteService(
    IQuoteRepository quoteRepository,
    IRequestRepository requestRepository,
    IPdfGenerator pdfGenerator,
    IQuoteDeliveryService deliveryService,
    IOutboundEmailRepository outboundEmailRepository) : IQuoteService
{
    public async Task<IReadOnlyList<QuoteResponse>> ListAsync(
        Guid requestId,
        Guid actorId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, actorId, role, tracking: false, manage: false, cancellationToken);
        var canManage = RequestAccess.CanManageQuotes(request, actorId, role);
        var emails = await outboundEmailRepository.ListForRequestAsync(requestId, cancellationToken);
        return (await quoteRepository.ListForRequestAsync(requestId, cancellationToken))
            .Select(quote => Map(quote, canManage, emails))
            .ToArray();
    }

    public async Task<QuoteResponse> GetAsync(
        Guid requestId,
        Guid quoteId,
        Guid actorId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, actorId, role, tracking: false, manage: false, cancellationToken);
        var quote = await LoadQuoteAsync(requestId, quoteId, cancellationToken);
        return await MapAsync(quote, RequestAccess.CanManageQuotes(request, actorId, role), cancellationToken);
    }

    /// <summary>
    /// Opens a quotation. From a sent, still open quotation this is a counter offer: a new revision that starts with
    /// the previous prices and replaces it.
    /// </summary>
    public async Task<QuoteResponse> CreateAsync(
        Guid requestId,
        Guid actorId,
        string role,
        CreateQuoteRequest createRequest,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, actorId, role, tracking: true, manage: true, cancellationToken);
        if (request.Status is not (JobStatus.RequestReceived or JobStatus.QuoteDraft or JobStatus.ClientCountered or JobStatus.QuoteSent))
            throw new InvalidOperationException("A quote can only be prepared for a new request, or revised while the client has not answered.");

        var existingQuotes = await quoteRepository.ListForRequestAsync(requestId, cancellationToken);
        if (existingQuotes.Any(quote => quote.IsEditable))
            throw new InvalidOperationException("This request already has a quotation being prepared.");

        Quote? previous = null;
        if (request.Status == JobStatus.QuoteSent)
        {
            var latest = existingQuotes.OrderBy(quote => quote.Version).LastOrDefault();
            previous = latest is null ? null : await quoteRepository.GetByIdAsync(latest.Id, cancellationToken);
            if (previous is null || previous.Status != QuoteStatus.Open || !previous.IsSent || !previous.IsCurrent)
                throw new InvalidOperationException("Only the current open quotation can be revised.");
            previous.Supersede();
        }

        if (request.Status != JobStatus.QuoteDraft)
            request.TransitionTo(JobStatus.QuoteDraft, actorId);

        var version = existingQuotes.Select(quote => quote.Version).DefaultIfEmpty(0).Max() + 1;
        var validForDays = createRequest.ValidForDays ?? previous?.ValidForDays ?? Quote.DefaultValidForDays;
        var clientName = string.IsNullOrWhiteSpace(request.ClientName) ? null : request.ClientName;
        var previousPrices = previous?.GetItems()
            .Where(item => item.RequestItemId is not null)
            .ToDictionary(item => item.RequestItemId!.Value, item => item.UnitPricePiastres);
        var items = request.Items
            .Select(item => ToQuoteItem(item, previousPrices is not null && previousPrices.TryGetValue(item.Id, out var price) ? price : null))
            .ToArray();
        var quote = Quote.Create(
            requestId,
            version,
            0,
            DateTimeOffset.UtcNow.AddDays(validForDays),
            actorId,
            requestNumber: previous is null ? null : Quote.NextRevisionNumber(previous.ProposalNumber, version),
            clientName: clientName);
        quote.SetDraftDetails(
            items,
            previous?.Message,
            Summarize(request.Items),
            clientName,
            previous?.GetNotes(),
            previous?.GetScopeOfWork(),
            validForDays);

        await quoteRepository.AddAsync(quote, cancellationToken);
        return await MapAsync(quote, canManage: true, cancellationToken);
    }

    public async Task<QuoteResponse> UpdateAsync(
        Guid requestId,
        Guid quoteId,
        Guid actorId,
        string role,
        UpdateQuoteRequest update,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, actorId, role, tracking: true, manage: true, cancellationToken);
        var quote = await LoadQuoteAsync(requestId, quoteId, cancellationToken);
        if (!quote.IsEditable)
            throw new InvalidOperationException("Only an open quote that was not sent can be edited.");
        if (request.Status != JobStatus.QuoteDraft)
            throw new InvalidOperationException("The request is not in the quote drafting stage.");

        var prices = MatchPricesToRequestItems(request, update.Items);
        var items = request.Items.Select(item => ToQuoteItem(item, prices[item.Id])).ToArray();
        quote.SetDraftDetails(
            items,
            update.Message,
            Summarize(request.Items),
            string.IsNullOrWhiteSpace(request.ClientName) ? null : request.ClientName,
            update.Notes,
            update.ScopeOfWork,
            update.ValidForDays);
        if (update.ProposalNumber is not null)
            quote.SetProposalNumber(update.ProposalNumber);

        await quoteRepository.SaveAsync(cancellationToken);
        return await MapAsync(quote, canManage: true, cancellationToken);
    }

    public async Task<PdfFile> GetPdfAsync(
        Guid requestId,
        Guid quoteId,
        Guid actorId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, actorId, role, tracking: false, manage: false, cancellationToken);
        var quote = await LoadQuoteAsync(requestId, quoteId, cancellationToken);
        return pdfGenerator.Generate(QuotationPdfModelFactory.Create(quote, request, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Replies with the offer (a first offer or a counter offer): the PDF is generated and emailed to the sender of the
    /// original request, and only after the mail server accepted it is the quotation marked as sent.
    /// </summary>
    public async Task<QuoteResponse> SendAsync(
        Guid requestId,
        Guid quoteId,
        Guid actorId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, actorId, role, tracking: true, manage: true, cancellationToken);
        var quote = await LoadQuoteAsync(requestId, quoteId, cancellationToken);
        if (!quote.IsEditable)
            throw new InvalidOperationException("Only an open quote that was not sent can be sent.");
        if (request.Status != JobStatus.QuoteDraft)
            throw new InvalidOperationException("The request is not in the quote drafting stage.");
        EnsureAllItemsPriced(quote);

        // Delivery comes first: if it fails nothing about the quote or the request has changed yet.
        await deliveryService.DeliverAsync(request, quote, OutboundEmailKind.Offer, cancellationToken);

        quote.Send();
        request.TransitionTo(JobStatus.QuoteSent, actorId);
        await quoteRepository.SaveAsync(cancellationToken);
        return await MapAsync(quote, canManage: true, cancellationToken);
    }

    /// <summary>
    /// Accepts the request: the quotation is closed as accepted, and its PDF is emailed to the sender of the original
    /// request. Works on a draft (which is sent at the same time) and on a quotation that was already sent.
    /// </summary>
    public async Task<QuoteResponse> AcceptAsync(
        Guid requestId,
        Guid quoteId,
        Guid actorId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, actorId, role, tracking: true, manage: true, cancellationToken);
        var quote = await LoadQuoteAsync(requestId, quoteId, cancellationToken);
        if (quote.Status != QuoteStatus.Open || !quote.IsCurrent)
            throw new InvalidOperationException("Only the current open quote can be accepted.");
        var expectedStatus = quote.IsSent ? JobStatus.QuoteSent : JobStatus.QuoteDraft;
        if (request.Status != expectedStatus)
            throw new InvalidOperationException("The request is not in the right stage to accept this quote.");
        if (!quote.IsSent) EnsureAllItemsPriced(quote);

        await deliveryService.DeliverAsync(request, quote, OutboundEmailKind.Accept, cancellationToken);

        if (!quote.IsSent)
        {
            quote.Send();
            request.TransitionTo(JobStatus.QuoteSent, actorId);
        }

        quote.RecordClientDecision(QuoteStatus.Accepted);
        request.TransitionTo(JobStatus.QuoteAccepted, actorId);
        await quoteRepository.SaveAsync(cancellationToken);
        return await MapAsync(quote, canManage: true, cancellationToken);
    }

    /// <summary>Records the outcome of a sent quotation (accepted or rejected) without sending any email.</summary>
    public async Task<QuoteResponse> RecordDecisionAsync(
        Guid requestId,
        Guid quoteId,
        Guid actorId,
        string role,
        QuoteDecisionRequest decision,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, actorId, role, tracking: true, manage: true, cancellationToken);
        var quote = await LoadQuoteAsync(requestId, quoteId, cancellationToken);
        quote.RecordClientDecision(decision.Decision, decision.Note);
        var status = decision.Decision switch
        {
            QuoteStatus.Accepted => JobStatus.QuoteAccepted,
            QuoteStatus.Rejected => JobStatus.QuoteRejected,
            _ => throw new ArgumentException("Decision must be accepted or rejected.", nameof(decision))
        };
        request.TransitionTo(status, actorId, decision.Note);
        await quoteRepository.SaveAsync(cancellationToken);
        return await MapAsync(quote, canManage: true, cancellationToken);
    }

    /// <summary>Loads the request when the actor may see it, and, for changes, when they may manage its quotes.</summary>
    private async Task<InspectionRequest> LoadRequestAsync(
        Guid requestId,
        Guid actorId,
        string role,
        bool tracking,
        bool manage,
        CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role))
            throw new KeyNotFoundException("Request was not found.");
        if (manage && !RequestAccess.CanManageQuotes(request, actorId, role))
            throw new AuthException("forbidden", "Only the coordinator that owns the request can change its quotes.", 403);
        return request;
    }

    private async Task<Quote> LoadQuoteAsync(Guid requestId, Guid quoteId, CancellationToken cancellationToken)
    {
        var quote = await quoteRepository.GetByIdAsync(quoteId, cancellationToken);
        return quote is null || quote.RequestId != requestId
            ? throw new KeyNotFoundException("Quote was not found for this request.")
            : quote;
    }

    private static void EnsureAllItemsPriced(Quote quote)
    {
        var unpriced = quote.GetUnpricedItems();
        if (unpriced.Count > 0)
            throw new ArgumentException(
                "Set a price for every item first: " +
                string.Join(", ", unpriced.Select(item => item.Description ?? item.Type)) + ".");
    }

    // Every request item must be priced exactly once, and nothing else may appear.
    private static Dictionary<Guid, long?> MatchPricesToRequestItems(
        InspectionRequest request,
        IReadOnlyList<UpdateQuoteItem> submitted)
    {
        var duplicates = submitted.GroupBy(item => item.RequestItemId).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        if (duplicates.Count > 0)
            throw new ArgumentException($"Each item can only appear once: {string.Join(", ", duplicates)}.");

        var requestItemIds = request.Items.Select(item => item.Id).ToHashSet();
        var unknown = submitted.Where(item => !requestItemIds.Contains(item.RequestItemId)).Select(item => item.RequestItemId).ToList();
        if (unknown.Count > 0)
            throw new ArgumentException($"These items do not belong to the request: {string.Join(", ", unknown)}.");

        var missing = request.Items.Where(item => submitted.All(line => line.RequestItemId != item.Id)).ToList();
        if (missing.Count > 0)
            throw new ArgumentException(
                "Every item of the request must be in the quotation. Missing: " +
                string.Join(", ", missing.Select(item => $"{item.Name} ({item.Id})")) + ".");

        return submitted.ToDictionary(item => item.RequestItemId, item => item.UnitPricePiastres);
    }

    private static QuoteItem ToQuoteItem(RequestItem item, long? unitPricePiastres) => QuoteItem.Create(
        item.Id,
        item.Type ?? item.Name,
        item.Quantity,
        unitPricePiastres,
        Quote.DefaultPricingUnit,
        item.Name,
        item.PrNumber);

    private static string Summarize(IEnumerable<RequestItem> items) =>
        string.Join(", ", items.Select(item => $"{item.Name} x {item.Quantity}"));

    private async Task<QuoteResponse> MapAsync(Quote quote, bool canManage, CancellationToken cancellationToken) =>
        Map(quote, canManage, await outboundEmailRepository.ListForRequestAsync(quote.RequestId, cancellationToken));

    private static QuoteResponse Map(Quote quote, bool canManage, IReadOnlyList<OutboundEmailSummary> emails)
    {
        var items = quote.GetItems();
        return new QuoteResponse(
            quote.Id,
            quote.RequestId,
            quote.Version,
            quote.AmountPiastres,
            quote.ValidUntil,
            quote.Status,
            quote.ClientReply,
            quote.CreatedAt,
            quote.ProposalNumber,
            quote.ClientName,
            quote.ValidForDays,
            quote.Message,
            items.Select(item => new QuoteItemResponse(
                item.RequestItemId,
                item.Description ?? item.Type,
                item.Type,
                item.PrNumber,
                item.Quantity,
                item.UnitPricePiastres,
                item.PricingUnit,
                item.TotalPiastres)).ToArray(),
            quote.GetScopeOfWork(),
            quote.GetNotes(),
            quote.GetUnpricedItems().Where(item => item.RequestItemId is not null).Select(item => item.RequestItemId!.Value).ToArray(),
            items.Sum(item => item.Quantity),
            canManage && quote.IsEditable,
            quote.IsExpired,
            quote.SentAt,
            quote.UpdatedAt,
            quote.SupersededAt,
            emails.Where(email => email.QuoteId == quote.Id)
                .OrderBy(email => email.CreatedAt)
                .Select(email => new QuoteEmailResponse(
                    email.Kind, email.Status, email.ToAddress, email.Attempts, email.SentAt, email.LastError))
                .ToArray());
    }
}
