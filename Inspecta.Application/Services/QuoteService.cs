using Inspecta.Application.Contracts;
using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Contracts.Requests;
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
    IOutboundEmailRepository outboundEmailRepository,
    IUserRepository userRepository,
    IRequestEquipmentRegistrar? equipmentRegistrar = null,
    ICompanyBrandingProvider? companyBranding = null,
    IClientReplyRepository? clientReplies = null) : IQuoteService
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
        var replies = await RepliesAsync(requestId, canManage, cancellationToken);
        return (await quoteRepository.ListForRequestAsync(requestId, cancellationToken))
            .Select(quote => Map(quote, canManage, emails, quote.IsCurrent ? replies : null))
            .ToArray();
    }

    public async Task<IReadOnlyList<QuoteSummaryResponse>> ListVisibleAsync(
        Guid actorId,
        string role,
        QuoteListQuery query,
        CancellationToken cancellationToken = default)
    {
        // An inspector only gets the accepted quotations of their own jobs, without any price.
        var isInspector = role == "inspector";
        var pageSize = Math.Clamp(query.PageSize, 1, QuoteListQuery.MaxPageSize);
        var page = Math.Max(query.Page, 1);
        var (createdFrom, createdUntil) = DateRange.Bounds(query.From, query.To);
        var filter = isInspector
            ? new QuoteListFilter(
                InspectorId: actorId,
                Open: false,
                Accepted: true,
                Search: string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
                Skip: (page - 1) * pageSize,
                Take: pageSize)
            : new QuoteListFilter(
            CoordinatorId: role == "coordinator" ? actorId : null,
            ManagerId: role == "admin" ? query.ManagerId : null,
            InspectorId: query.InspectorId,
            Assigned: query.Assigned,
            Open: query.Stage switch
            {
                QuoteStage.Draft or QuoteStage.Sent => true,
                QuoteStage.Accepted or QuoteStage.Rejected => false,
                _ => null
            },
            Sent: query.Stage switch
            {
                QuoteStage.Draft => false,
                QuoteStage.Sent => true,
                _ => null
            },
            Accepted: query.Stage switch
            {
                QuoteStage.Accepted => true,
                QuoteStage.Rejected => false,
                _ => null
            },
            CurrentOnly: !query.IncludeSuperseded,
            Search: string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Skip: (page - 1) * pageSize,
            Take: pageSize);
        filter = filter with { CreatedFrom = createdFrom, CreatedUntil = createdUntil };
        return await ListSummariesAsync(filter, actorId, role, cancellationToken);
    }

    public async Task<IReadOnlyList<QuoteSummaryResponse>> ListQueueAsync(
        Guid actorId,
        string role,
        QuotationQueue? queue,
        QuotationQueueQuery query,
        CancellationToken cancellationToken = default)
    {
        // Same visibility as the quotation list; the queue only adds which accepted quotations are returned.
        var pageSize = Math.Clamp(query.PageSize, 1, QuoteListQuery.MaxPageSize);
        var page = Math.Max(query.Page, 1);
        var isInspector = role == "inspector";
        var (queueFrom, queueUntil) = DateRange.Bounds(query.From, query.To);
        var filter = new QuoteListFilter(
            CoordinatorId: role == "coordinator" ? actorId : null,
            ManagerId: role == "admin" ? query.ManagerId : null,
            InspectorId: isInspector ? actorId : query.InspectorId,
            Open: false,
            Accepted: true,
            Search: string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            Skip: (page - 1) * pageSize,
            Take: pageSize,
            RequestStatuses: queue is { } one
                ? QuotationQueues.Statuses(one)
                : QuotationQueues.AllStatuses,
            CreatedFrom: queueFrom,
            CreatedUntil: queueUntil);
        return await ListSummariesAsync(filter, actorId, role, cancellationToken);
    }

    private async Task<IReadOnlyList<QuoteSummaryResponse>> ListSummariesAsync(
        QuoteListFilter filter,
        Guid actorId,
        string role,
        CancellationToken cancellationToken)
    {
        var isInspector = role == "inspector";
        var quotes = (await quoteRepository.ListAsync(filter, cancellationToken))
            .Where(quote => quote.Request is not null)
            .ToArray();
        var certificateStates = await quoteRepository.ListCertificateStatesAsync(
            quotes.Select(quote => quote.Request!.Id).Distinct().ToArray(), cancellationToken);
        var inspectorNames = await userRepository.GetFullNamesAsync(
            quotes.Select(quote => quote.Request!.InspectorId).OfType<Guid>().Distinct().ToArray(), cancellationToken);

        return quotes
            .Select(quote => MapSummary(
                quote,
                quote.Request!,
                !isInspector && RequestAccess.CanManageQuotes(quote.Request!, actorId, role),
                showPrices: !isInspector,
                quote.Request!.InspectorId is Guid id && inspectorNames.TryGetValue(id, out var name) ? name : null,
                certificateStates.TryGetValue(quote.Request!.Id, out var states) ? states : null))
            .ToArray();
    }

    // How far the certificates of an assigned job are; not shown before the job is given to an inspector.
    private static Contracts.Certificates.MachineProgress? CertificateProgressOf(InspectionRequest request, IReadOnlyList<CertificateState>? loaded)
    {
        if (QuotationQueues.Of(request.Status) is not (QuotationQueue.Assigned or QuotationQueue.Certify)) return null;
        // The list reads only the states of the certificates; a repository that already put the certificates on the request is read as before.
        var certificates = loaded ?? request.Certificates.Select(CertificateState.Of).ToArray();
        var states = request.Items
            .Select(item => MachineInspectionService.StatusOf(certificates.SingleOrDefault(value => value.RequestItemId == item.Id)))
            .ToArray();
        return new Contracts.Certificates.MachineProgress(
            states.Length,
            states.Count(state => state is Contracts.Certificates.MachineStatus.Submitted or Contracts.Certificates.MachineStatus.Approved or Contracts.Certificates.MachineStatus.Delivered),
            states.Count(state => state is Contracts.Certificates.MachineStatus.Approved or Contracts.Certificates.MachineStatus.Delivered),
            states.Count(state => state == Contracts.Certificates.MachineStatus.Delivered));
    }

    private static QuoteSummaryResponse MapSummary(
        Quote quote,
        InspectionRequest request,
        bool canManage,
        bool showPrices,
        string? inspectorName,
        IReadOnlyList<CertificateState>? certificateStates = null)
    {
        var items = quote.GetItems();
        return new QuoteSummaryResponse(
            quote.Id,
            quote.RequestId,
            quote.Version,
            quote.Status switch
            {
                QuoteStatus.Accepted => QuoteStage.Accepted,
                QuoteStatus.Rejected => QuoteStage.Rejected,
                _ => quote.IsSent ? QuoteStage.Sent : QuoteStage.Draft
            },
            quote.Status,
            request.Status,
            quote.QuotationNumber,
            quote.ProposalNumber,
            quote.ClientName ?? request.ClientName,
            request.Location,
            showPrices ? quote.AmountPiastres : null,
            items.Where(item => !item.IsManDay).Sum(item => item.Quantity),
            showPrices ? quote.GetUnpricedItems().Count : 0,
            quote.ValidUntil,
            quote.IsExpired,
            quote.IsCurrent,
            canManage && quote.IsEditable,
            quote.SentAt,
            quote.CreatedAt,
            quote.UpdatedAt,
            request.InspectorId,
            inspectorName,
            request.ScheduledAt,
            QuotationQueues.Of(request.Status),
            CertificateProgressOf(request, certificateStates),
            request.HasUnreadClientReply,
            request.Kind);
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

    // A sent quotation waits for the client in one of these: the first offer, a counter offer, or after the client answered.
    private static bool IsWaitingForClient(JobStatus status) =>
        status is JobStatus.QuoteSent or JobStatus.CounterOfferSent or JobStatus.ClientCountered;

    // The first offer is quote_sent; a revision (the second or later) is a counter offer.
    private static JobStatus SentStatusOf(Quote quote) => quote.Version > 1 ? JobStatus.CounterOfferSent : JobStatus.QuoteSent;

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
        if (request.Status is not (JobStatus.RequestReceived or JobStatus.QuoteDraft) && !IsWaitingForClient(request.Status))
            throw new InvalidOperationException("A quote can only be prepared for a new request, or revised while the client has not answered.");

        var existingQuotes = await quoteRepository.ListForRequestAsync(requestId, cancellationToken);
        if (existingQuotes.Any(quote => quote.IsEditable))
            throw new InvalidOperationException("This request already has a quotation being prepared.");

        Quote? previous = null;
        if (IsWaitingForClient(request.Status))
        {
            var latest = existingQuotes.OrderBy(quote => quote.Version).LastOrDefault();
            previous = latest is null ? null : await quoteRepository.GetByIdAsync(latest.Id, cancellationToken);
            if (previous is null || previous.Status != QuoteStatus.Open || !previous.IsSent || !previous.IsCurrent)
                throw new InvalidOperationException("Only the current open quotation can be revised.");
            previous.Supersede();
        }

        if (request.Status != JobStatus.QuoteDraft)
            request.TransitionTo(JobStatus.QuoteDraft, actorId);

        var quote = await BuildNewDraftAsync(request, existingQuotes, previous, createRequest.ValidForDays, actorId, cancellationToken);
        await quoteRepository.AddAsync(quote, cancellationToken);
        return await MapAsync(quote, canManage: true, cancellationToken);
    }

    /// <summary>
    /// The client's reply needs nothing from the coordinator (a thank-you, a question already answered): it is cleared and the request goes back
    /// to waiting for the client (<c>quote_sent</c>, or <c>counter_offer_sent</c> for a revision).
    /// </summary>
    public async Task<QuoteResponse> DismissClientReplyAsync(Guid requestId, Guid quoteId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, actorId, role, tracking: true, manage: true, cancellationToken);
        var quote = await LoadQuoteAsync(requestId, quoteId, cancellationToken);
        var pending = clientReplies is null ? [] : await clientReplies.ListForRequestAsync(requestId, cancellationToken);
        if (request.Status != JobStatus.ClientCountered && !request.HasUnreadClientReply && pending.Count == 0)
            throw new InvalidOperationException("There is no reply from the client to dismiss.");

        var current = (await quoteRepository.ListForRequestAsync(requestId, cancellationToken))
            .Where(item => item.IsSent && item.IsCurrent && item.Status == QuoteStatus.Open)
            .OrderBy(item => item.Version)
            .LastOrDefault();
        request.DismissClientReply(actorId, current is null ? JobStatus.QuoteSent : SentStatusOf(current));
        await ClearRepliesAsync(request, cancellationToken);
        await quoteRepository.SaveAsync(cancellationToken);
        return await MapAsync(quote, canManage: true, cancellationToken);
    }

    /// <summary>
    /// Takes a sent quotation back to the quotations being prepared, by hand (for example after the price was talked over by phone): a new
    /// revision that starts with its prices and replaces it, and the request goes back to <c>quote_draft</c>. The revision that was sent stays as it was.
    /// </summary>
    public async Task<QuoteResponse> ReopenAsync(Guid requestId, Guid quoteId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, actorId, role, tracking: true, manage: true, cancellationToken);
        var sent = await LoadQuoteAsync(requestId, quoteId, cancellationToken);
        if (!IsWaitingForClient(request.Status) || sent.Status != QuoteStatus.Open || !sent.IsSent || !sent.IsCurrent)
            throw new InvalidOperationException("Only the current sent quotation, while the client has not accepted or rejected it, can be taken back.");

        var existingQuotes = await quoteRepository.ListForRequestAsync(requestId, cancellationToken);
        if (existingQuotes.Any(quote => quote.IsEditable))
            throw new InvalidOperationException("This request already has a quotation being prepared.");

        sent.Supersede();
        request.TransitionTo(JobStatus.QuoteDraft, actorId);
        var quote = await BuildNewDraftAsync(request, existingQuotes, sent, null, actorId, cancellationToken);
        await quoteRepository.AddAsync(quote, cancellationToken);
        return await MapAsync(quote, canManage: true, cancellationToken);
    }

    /// <summary>
    /// Saves the request screen and opens the quotation in the same transaction. The quotation is created from the saved
    /// items, or, when a draft already exists, refreshed from them keeping its prices. The request must be complete.
    /// </summary>
    public async Task<RequestResponse> SaveRequestAndPrepareQuotationAsync(
        Guid requestId,
        Guid actorId,
        string role,
        UpdateRequestDetailsRequest details,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, actorId, role, tracking: true, manage: true, cancellationToken);
        if (request.CoordinatorId != actorId)
            throw new AuthException("forbidden", "Only the coordinator that owns the request can edit it.", 403);

        var changes = await new RequestDetailsEditor(requestRepository, equipmentRegistrar).ApplyAsync(request, details, cancellationToken);
        var missing = request.GetMissingFields();

        // Left to the server, the quotation opens as soon as the request is complete and a real request; until then it is a plain save.
        if (details.PrepareQuotation is null && (missing.Count > 0 || request.Triage != RequestTriage.Request))
        {
            await requestRepository.SaveRevisionAsync(request, changes, cancellationToken);
            return RequestResponseMapper.Map(request);
        }

        if (missing.Count > 0)
            throw new AuthException(
                "request_incomplete",
                $"Complete the request before preparing the quotation. Missing: {string.Join(", ", missing)}.",
                409);

        var existingQuotes = await quoteRepository.ListForRequestAsync(request.Id, cancellationToken);
        var draftId = existingQuotes.FirstOrDefault(quote => quote.IsEditable)?.Id;
        var draft = draftId is Guid id ? await quoteRepository.GetByIdAsync(id, cancellationToken) : null;
        if (draft is not null)
        {
            RefreshDraftItems(draft, request);
        }
        else
        {
            if (request.Status != JobStatus.QuoteDraft)
                request.TransitionTo(JobStatus.QuoteDraft, actorId);
            draft = await BuildNewDraftAsync(request, existingQuotes, null, null, actorId, cancellationToken);
        }

        // One save for the request, its item changes, the new machines and the quotation.
        requestRepository.StageRevision(changes);
        if (draftId is null) await quoteRepository.AddAsync(draft, cancellationToken);
        else await quoteRepository.SaveAsync(cancellationToken);

        return RequestResponseMapper.Map(request) with { Quotation = await MapAsync(draft, canManage: true, cancellationToken) };
    }

    private async Task<Quote> BuildNewDraftAsync(
        InspectionRequest request,
        IReadOnlyList<Quote> existingQuotes,
        Quote? previous,
        int? requestedValidForDays,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var version = existingQuotes.Select(quote => quote.Version).DefaultIfEmpty(0).Max() + 1;
        var validForDays = requestedValidForDays ?? previous?.ValidForDays ?? Quote.DefaultValidForDays;
        var clientName = string.IsNullOrWhiteSpace(request.ClientName) ? null : request.ClientName;
        var previousPrices = previous?.GetItems()
            .Where(item => item.RequestItemId is not null)
            .ToDictionary(item => item.RequestItemId!.Value, item => item.UnitPricePiastres);
        var items = QuoteLines(
            request,
            item => previousPrices is not null && previousPrices.TryGetValue(item.Id, out var price) ? price : null,
            previous?.GetItems().FirstOrDefault(line => line.IsManDay)?.UnitPricePiastres);
        var sourceEmail = previous is null ? await requestRepository.GetEmailAsync(request.Id, cancellationToken) : null;
        var contactName = previous?.ContactName ?? sourceEmail?.FromName;
        var quote = Quote.Create(
            request.Id,
            version,
            0,
            DateTimeOffset.UtcNow.AddDays(validForDays),
            actorId,
            requestNumber: previous is null ? null : Quote.NextRevisionNumber(previous.ProposalNumber, version),
            clientName: clientName,
            quotationNumber: previous?.QuotationNumber,
            contactName: contactName);
        quote.SetDraftDetails(
            items,
            previous?.Message,
            Summarize(request),
            clientName,
            previous?.GetNotes() ?? QuotationDefaults.StandardNotes,
            previous?.GetScopeOfWork(),
            validForDays);
        // A revision starts from the previous discount, like prices and notes; the coordinator can change it.
        if (previous is { DiscountPercent: > 0m })
            quote.SetDiscount(previous.DiscountPercent);
        return quote;
    }

    // The draft follows the request items; items that were already priced keep their price.
    private static void RefreshDraftItems(Quote draft, InspectionRequest request)
    {
        var prices = draft.GetItems()
            .Where(item => item.RequestItemId is not null)
            .ToDictionary(item => item.RequestItemId!.Value, item => item.UnitPricePiastres);
        draft.SetDraftDetails(
            QuoteLines(request, item => prices.TryGetValue(item.Id, out var price) ? price : null, draft.GetItems().FirstOrDefault(line => line.IsManDay)?.UnitPricePiastres),
            draft.Message,
            Summarize(request),
            string.IsNullOrWhiteSpace(request.ClientName) ? null : request.ClientName);
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

        // Checked first, so a wrong discount changes nothing.
        if (update.DiscountPercent is { } discount) Quote.ValidateDiscount(discount);
        var prices = MatchPricesToRequestItems(request, update.Items);
        var items = QuoteLines(request, item => prices[item.Id], update.ManDayPricePiastres);
        quote.SetDraftDetails(
            items,
            update.Message,
            Summarize(request),
            string.IsNullOrWhiteSpace(request.ClientName) ? null : request.ClientName,
            update.Notes,
            update.ScopeOfWork,
            update.ValidForDays,
            update.ContactName);
        if (update.DiscountPercent is { } percent)
            quote.SetDiscount(percent);
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
        var coordinator = request.CoordinatorId is Guid coordinatorId
            ? await userRepository.GetByIdAsync(coordinatorId, cancellationToken)
            : null;
        var company = companyBranding is null ? null : await companyBranding.GetAsync(request.ManagerId, cancellationToken);
        return pdfGenerator.Generate(QuotationPdfModelFactory.Create(
            quote, request, coordinator?.FullName ?? string.Empty, DateTimeOffset.UtcNow, company));
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
        SendQuoteEmailRequest? email = null,
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
        await deliveryService.DeliverAsync(request, quote, OutboundEmailKind.Offer, ToDraft(email), cancellationToken);

        quote.Send();
        request.TransitionTo(SentStatusOf(quote), actorId);

        // A new offer is out: what the client wrote about the previous one is cleared.
        await ClearRepliesAsync(request, cancellationToken);
        await quoteRepository.SaveAsync(cancellationToken);
        return await MapAsync(quote, canManage: true, cancellationToken);
    }

    /// <summary>
    /// The email that would carry the quotation PDF (the same one SendAsync or AcceptAsync sends), with the coordinator's edits
    /// applied. Sends nothing; says whether the Send button works and why not.
    /// </summary>
    public async Task<QuoteEmailPreviewResponse> PreviewEmailAsync(
        Guid requestId,
        Guid quoteId,
        string action,
        Guid actorId,
        string role,
        SendQuoteEmailRequest? email = null,
        CancellationToken cancellationToken = default)
    {
        var accepting = string.Equals(action, "accept", StringComparison.OrdinalIgnoreCase);
        if (!accepting && !string.Equals(action, "send", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Action must be 'send' or 'accept'.", nameof(action));
        var request = await LoadRequestAsync(requestId, actorId, role, tracking: false, manage: true, cancellationToken);
        var quote = await LoadQuoteAsync(requestId, quoteId, cancellationToken);

        var blockers = new List<string>();
        if (accepting)
        {
            if (quote.Status != QuoteStatus.Open || !quote.IsCurrent) blockers.Add("Only the current open quote can be accepted.");
            else if (quote.IsSent ? !IsWaitingForClient(request.Status) : request.Status != JobStatus.QuoteDraft) blockers.Add("The request is not in the right stage to accept this quote.");
            else if (!quote.IsSent) AddUnpriced(quote, blockers);
        }
        else
        {
            if (!quote.IsEditable) blockers.Add("Only an open quote that was not sent can be sent.");
            else if (request.Status != JobStatus.QuoteDraft) blockers.Add("The request is not in the quote drafting stage.");
            else AddUnpriced(quote, blockers);
        }

        var plan = await deliveryService.PlanAsync(request, quote, accepting ? OutboundEmailKind.Accept : OutboundEmailKind.Offer, ToDraft(email), cancellationToken);
        blockers.AddRange(plan.Blockers);
        return new QuoteEmailPreviewResponse(
            accepting ? "accept" : "send",
            plan.EmailEnabled,
            plan.ToAddress,
            plan.ToName,
            plan.Cc,
            plan.Subject,
            plan.Body,
            plan.FromAddress,
            plan.AttachmentName,
            $"/api/v1/requests/{requestId}/quotes/{quoteId}/pdf",
            plan.RepliesToOriginalEmail,
            plan.EmailEnabled ? null : "Email delivery is switched off: sending only changes the quotation, no email goes out.",
            blockers.Count == 0,
            blockers,
            plan.BodyHtml,
            plan.ExtraAttachmentNames);
    }

    private static QuoteEmailDraft? ToDraft(SendQuoteEmailRequest? email) =>
        email is null ? null : new QuoteEmailDraft(email.SentTo, email.Cc, email.Subject, email.Message, ExtraEmailAttachments.Decode(email.Attachments));

    private static void AddUnpriced(Quote quote, List<string> blockers)
    {
        var unpriced = quote.GetUnpricedItems();
        if (unpriced.Count > 0)
            blockers.Add("Set a price for every item first: " + string.Join(", ", unpriced.Select(item => item.Description ?? item.Type)) + ".");
    }

    /// <summary>
    /// Turns the request down with a reason, from any stage before acceptance. The open quotation, if there is one, is closed as
    /// rejected too (even if it was never sent). No email is sent.
    /// </summary>
    public async Task<RequestResponse> RejectRequestAsync(
        Guid requestId,
        Guid actorId,
        string role,
        RejectRequestRequest rejection,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, actorId, role, tracking: true, manage: true, cancellationToken);
        if (string.IsNullOrWhiteSpace(rejection.Reason))
            throw new ArgumentException("A rejection reason is required.");

        // The request transition validates the stage (and that the email was confirmed) before anything else changes.
        request.TransitionTo(JobStatus.QuoteRejected, actorId, rejection.Reason);
        await ClearRepliesAsync(request, cancellationToken);
        var open = (await quoteRepository.ListForRequestAsync(requestId, cancellationToken))
            .FirstOrDefault(quote => quote.Status == QuoteStatus.Open && quote.IsCurrent);
        if (open is not null)
            (await quoteRepository.GetByIdAsync(open.Id, cancellationToken))?.Reject(rejection.Reason);

        await quoteRepository.SaveAsync(cancellationToken);
        return RequestResponseMapper.Map(request);
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
        SendQuoteEmailRequest? email = null,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, actorId, role, tracking: true, manage: true, cancellationToken);
        var quote = await LoadQuoteAsync(requestId, quoteId, cancellationToken);
        if (quote.Status != QuoteStatus.Open || !quote.IsCurrent)
            throw new InvalidOperationException("Only the current open quote can be accepted.");
        if (quote.IsSent ? !IsWaitingForClient(request.Status) : request.Status != JobStatus.QuoteDraft)
            throw new InvalidOperationException("The request is not in the right stage to accept this quote.");
        if (!quote.IsSent) EnsureAllItemsPriced(quote);

        await deliveryService.DeliverAsync(request, quote, OutboundEmailKind.Accept, ToDraft(email), cancellationToken);

        if (!quote.IsSent)
        {
            quote.Send();
            request.TransitionTo(SentStatusOf(quote), actorId);
        }

        quote.RecordClientDecision(QuoteStatus.Accepted);
        request.TransitionTo(JobStatus.Unassigned, actorId);
        await ClearRepliesAsync(request, cancellationToken);
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
            QuoteStatus.Accepted => JobStatus.Unassigned,
            QuoteStatus.Rejected => JobStatus.QuoteRejected,
            _ => throw new ArgumentException("Decision must be accepted or rejected.", nameof(decision))
        };
        request.TransitionTo(status, actorId, decision.Note);
        await ClearRepliesAsync(request, cancellationToken);
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

    // The lines of a quotation: one for each machine of the request, then the man day when the request asks for one.
    private static QuoteItem[] QuoteLines(InspectionRequest request, Func<RequestItem, long?> priceOf, long? manDayPricePiastres)
    {
        var lines = request.Items.Select(item => ToQuoteItem(item, priceOf(item))).ToList();
        if (request.ManDayDays is > 0 and var days) lines.Add(QuoteItem.ManDay(days, manDayPricePiastres));
        return lines.ToArray();
    }

    private static string Summarize(InspectionRequest request) =>
        string.Join(", ", request.Items.Select(item => $"{item.Name} x {item.Quantity}")
            .Concat(request.ManDayDays is > 0 and var days ? [$"{QuoteItem.ManDayType} x {days}"] : []));

    private async Task<QuoteResponse> MapAsync(Quote quote, bool canManage, CancellationToken cancellationToken) =>
        Map(
            quote,
            canManage,
            await outboundEmailRepository.ListForRequestAsync(quote.RequestId, cancellationToken),
            quote.IsCurrent ? await RepliesAsync(quote.RequestId, canManage, cancellationToken) : null);

    // What the client wrote back since the last offer went out; only the people who manage the quotations see it.
    private async Task<IReadOnlyList<ClientReplyResponse>> RepliesAsync(Guid requestId, bool canManage, CancellationToken cancellationToken)
    {
        if (!canManage || clientReplies is null) return [];
        return (await clientReplies.ListForRequestAsync(requestId, cancellationToken)).Select(MapReply).ToArray();
    }

    private static ClientReplyResponse MapReply(ClientReply reply) => new(
        reply.Id,
        reply.QuoteId,
        reply.FromAddress,
        reply.FromName,
        reply.Subject,
        reply.SentAt ?? reply.ReceivedAt,
        EmailTextCleaner.NewText(null, reply.TextBody, reply.HtmlBody),
        reply.HtmlBody,
        reply.GetAttachments().Select(file => new ClientReplyAttachmentResponse(file.FileName, file.ContentType, file.Size)).ToArray());

    // The replies end with the exchange they belong to: the next offer, an acceptance or a rejection.
    private async Task ClearRepliesAsync(InspectionRequest request, CancellationToken cancellationToken)
    {
        if (clientReplies is not null) await clientReplies.RemoveForRequestAsync(request.Id, cancellationToken);
        request.MarkClientReplyRead();
    }

    private static QuoteResponse Map(
        Quote quote, bool canManage, IReadOnlyList<OutboundEmailSummary> emails, IReadOnlyList<ClientReplyResponse>? replies = null)
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
            items.Where(item => !item.IsManDay).Sum(item => item.Quantity),
            canManage && quote.IsEditable,
            quote.IsExpired,
            quote.SentAt,
            quote.UpdatedAt,
            quote.SupersededAt,
            emails.Where(email => email.QuoteId == quote.Id)
                .OrderBy(email => email.CreatedAt)
                .Select(email => new QuoteEmailResponse(
                    email.Kind, email.Status, email.ToAddress, email.Attempts, email.SentAt, email.LastError,
                    email.Cc?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [], email.Subject))
                .ToArray(),
            quote.ContactName,
            quote.QuotationNumber,
            replies ?? [],
            quote.SubtotalPiastres,
            quote.DiscountPercent,
            quote.DiscountPiastres);
    }
}
