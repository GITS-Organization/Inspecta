using Inspecta.Application.Authorization;
using Inspecta.Application.Contracts;
using Inspecta.Application.Contracts.Invoices;
using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Pdf;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Domain.Exceptions;

namespace Inspecta.Application.Services;

/// <summary>
/// Invoices and their payments. The rules of an invoice live in the domain; this decides who may do what and when: the coordinator who owns
/// the request works on it until it is handed over, the financial manager after, the platform owner always, and the general manager only looks.
/// </summary>
public sealed class InvoiceService(
    IInvoiceRepository invoices,
    IRequestRepository requests,
    IQuoteRepository quotes,
    IUserRepository users,
    IInvoiceDeliveryService deliveryService,
    IPdfGenerator pdfGenerator,
    ICompanyBrandingProvider? companyBranding = null,
    IClientRepository? clients = null) : IInvoiceService
{
    public const int MaxPageSize = 100;

    private static readonly string[] PaymentStates = ["unpaid", "partial", "paid"];

    public async Task<PagedList<InvoiceSummary>> ListAsync(Guid actorId, string role, InvoiceListQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var paymentState = string.IsNullOrWhiteSpace(query.PaymentState) ? null : query.PaymentState.Trim().ToLowerInvariant();
        if (paymentState is not null && !PaymentStates.Contains(paymentState))
            throw new ArgumentException("paymentState must be 'unpaid', 'partial' or 'paid'.");

        var result = await invoices.ListAsync(new InvoiceListFilter(
            Stage: ParseStage(query.Stage),
            PaymentState: paymentState,
            Search: query.Search,
            RequestId: query.RequestId,
            CoordinatorId: role == RoleNames.Coordinator ? actorId : null,
            ManagerId: role == RoleNames.Admin ? query.ManagerId : null,
            From: query.From,
            To: query.To,
            Skip: (page - 1) * size,
            Take: size), cancellationToken);
        return new PagedList<InvoiceSummary>(result.Items.Select(Summary).ToList(), result.Total);
    }

    public async Task<InvoiceDetail> GetAsync(Guid id, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var (invoice, request) = await LoadAsync(id, actorId, role, tracking: false, cancellationToken);
        return await DetailAsync(invoice, request, actorId, role, cancellationToken);
    }

    public async Task<InvoiceDetail> CreateForRequestAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await requests.GetByIdAsync(requestId, false, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role)) throw new KeyNotFoundException("Request was not found.");
        if (!RequestAccess.CanManageQuotes(request, actorId, role))
            throw new AuthException("forbidden", "Only the coordinator that owns the request can make its invoice.", 403);

        var accepted = (await quotes.ListForRequestAsync(requestId, cancellationToken))
            .Where(quote => quote.Status == QuoteStatus.Accepted && quote.IsCurrent)
            .OrderByDescending(quote => quote.Version)
            .FirstOrDefault()
            ?? throw new AuthException("no_accepted_quote", "The request has no accepted quotation to invoice.", 409);

        var existing = await invoices.GetByQuoteIdAsync(accepted.Id, cancellationToken);
        if (existing is not null) return await GetAsync(existing.Id, actorId, role, cancellationToken);

        var invoice = Invoice.CreateFor(
            accepted, request, actorId, InvoiceCalendar.Of(accepted.ClientRespondedAt ?? DateTimeOffset.UtcNow), "Made for a job accepted before invoices existed")
            ?? throw new AuthException("no_invoice_needed", "This quotation has nothing to pay (no price), so it needs no invoice.", 409);
        await invoices.AddAsync(invoice, cancellationToken);
        await invoices.SaveAsync(cancellationToken);
        return await DetailAsync(invoice, request, actorId, role, cancellationToken);
    }

    public async Task<InvoiceDetail> RecordPaymentAsync(
        Guid id, RecordPaymentRequest request, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var (invoice, job) = await LoadAsync(id, actorId, role, tracking: true, cancellationToken);
        EnsureCanRecord(invoice, job, actorId, role, reversing: false);
        invoice.RecordPayment(request.AmountPiastres, request.PaidOn, request.Label, actorId, InvoiceCalendar.Today());
        await invoices.SaveAsync(cancellationToken);
        return await DetailAsync(invoice, job, actorId, role, cancellationToken);
    }

    public async Task<InvoiceDetail> ReversePaymentAsync(
        Guid id, Guid paymentId, ReversePaymentRequest request, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var (invoice, job) = await LoadAsync(id, actorId, role, tracking: true, cancellationToken);
        EnsureCanRecord(invoice, job, actorId, role, reversing: true);
        invoice.ReversePayment(paymentId, request.Reason, actorId, InvoiceCalendar.Today(), allowClosed: role == RoleNames.Admin);
        await invoices.SaveAsync(cancellationToken);
        return await DetailAsync(invoice, job, actorId, role, cancellationToken);
    }

    public async Task<InvoiceDetail> HandOverAsync(Guid id, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var (invoice, job) = await LoadAsync(id, actorId, role, tracking: true, cancellationToken);
        EnsureCoordinatorOrOwner(job, actorId, role);
        EnsureDelivered(job);
        invoice.HandOver(actorId);
        await invoices.SaveAsync(cancellationToken);
        return await DetailAsync(invoice, job, actorId, role, cancellationToken);
    }

    public async Task<InvoiceDetail> CloseAsync(Guid id, CloseInvoiceRequest? request, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var (invoice, job) = await LoadAsync(id, actorId, role, tracking: true, cancellationToken);
        if (role == RoleNames.FinancialManager && invoice.Stage == InvoiceStage.Open)
            throw new InvoiceRuleException("wrong_stage", "The invoice is still with the coordinator.");
        if (string.IsNullOrWhiteSpace(request?.Reason)) invoice.Close(actorId);
        else invoice.CloseManually(actorId, request.Reason);
        await invoices.SaveAsync(cancellationToken);
        return await DetailAsync(invoice, job, actorId, role, cancellationToken);
    }

    public async Task<RevenueResponse> RevenueAsync(DateOnly? from, DateOnly? to, Guid? managerId, string role, CancellationToken cancellationToken = default)
    {
        var today = InvoiceCalendar.Today();
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;
        if (start > end) throw new ArgumentException("from cannot be after to.");

        var rows = await invoices.RevenueAsync(start, end, role == RoleNames.Admin ? managerId : null, cancellationToken);
        var byMonth = rows.GroupBy(row => $"{row.PaidOn:yyyy-MM}").OrderBy(group => group.Key)
            .Select(group => new RevenueByMonth(group.Key, group.Sum(row => row.AmountPiastres))).ToList();
        var byClient = rows.GroupBy(row => row.ClientId is Guid id ? id.ToString() : "name:" + row.ClientName.Trim().ToLowerInvariant())
            .Select(group => new RevenueByClient(group.First().ClientId, group.First().ClientName.Trim(), group.Sum(row => row.AmountPiastres)))
            .OrderByDescending(item => item.AmountPiastres).ThenBy(item => item.ClientName).ToList();
        return new RevenueResponse(start, end, rows.Sum(row => row.AmountPiastres), byMonth, byClient);
    }

    public async Task<PdfFile> GetPdfAsync(Guid id, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var (invoice, request) = await LoadAsync(id, actorId, role, tracking: false, cancellationToken);
        var client = clients is not null && invoice.ClientId is Guid clientId ? await clients.GetByIdAsync(clientId, false, cancellationToken) : null;
        var company = companyBranding is null ? null : await companyBranding.GetAsync(request?.ManagerId ?? invoice.ManagerId, cancellationToken);
        return pdfGenerator.Generate(InvoicePdfModelFactory.Create(invoice, client, company, InvoiceCalendar.Today()));
    }

    public async Task<QuoteEmailPreviewResponse> PreviewEmailAsync(
        Guid id, SendQuoteEmailRequest? email, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var (invoice, job) = await LoadAsync(id, actorId, role, tracking: false, cancellationToken);
        EnsureCoordinatorOrOwner(job, actorId, role);
        var plan = await deliveryService.PlanAsync(invoice, job!, ToDraft(email), cancellationToken);
        var blockers = new List<string>();
        if (invoice.Stage == InvoiceStage.Closed) blockers.Add("The invoice is closed.");
        if (job!.Status != JobStatus.SentToClient) blockers.Add("The certificates have not been delivered to the client yet.");
        blockers.AddRange(plan.Blockers);
        return new QuoteEmailPreviewResponse(
            "invoice", plan.EmailEnabled, plan.ToAddress, plan.ToName, plan.Cc, plan.Subject, plan.Body, plan.FromAddress, plan.AttachmentName,
            $"/api/v1/invoices/{invoice.Id}/pdf", plan.RepliesToOriginalEmail, null, blockers.Count == 0, blockers, plan.BodyHtml, plan.ExtraAttachmentNames);
    }

    public async Task<InvoiceDetail> SendEmailAsync(
        Guid id, SendQuoteEmailRequest? email, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var (invoice, job) = await LoadAsync(id, actorId, role, tracking: true, cancellationToken);
        EnsureCoordinatorOrOwner(job, actorId, role);
        if (invoice.Stage == InvoiceStage.Closed) throw new InvoiceRuleException("invoice_closed", "The invoice is closed.");
        EnsureDelivered(job);
        var sent = await deliveryService.SendAsync(invoice, job!, ToDraft(email), cancellationToken);
        invoice.MarkEmailed(actorId, $"Sent to {sent.ToAddress}");
        await invoices.SaveAsync(cancellationToken);
        return await DetailAsync(invoice, job, actorId, role, cancellationToken);
    }

    private static QuoteEmailDraft? ToDraft(SendQuoteEmailRequest? email) =>
        email is null ? null : new QuoteEmailDraft(email.SentTo, email.Cc, email.Subject, email.Message, ExtraEmailAttachments.Decode(email.Attachments));

    // ---- who may do what ----

    // Payments: the coordinator who owns the request while the invoice is open, the financial manager once it is with finance, the platform owner always.
    private static void EnsureCanRecord(Invoice invoice, InspectionRequest? job, Guid actorId, string role, bool reversing)
    {
        if (invoice.Stage == InvoiceStage.Closed && !(reversing && role == RoleNames.Admin))
            throw new InvoiceRuleException("invoice_closed", "The invoice is closed.");
        switch (role)
        {
            case RoleNames.Admin:
                return;
            case RoleNames.Coordinator:
                EnsureCoordinatorOrOwner(job, actorId, role);
                if (invoice.Stage != InvoiceStage.Open)
                    throw new InvoiceRuleException("wrong_stage", "The invoice is with finance: the financial manager records its payments now.");
                return;
            case RoleNames.FinancialManager:
                if (invoice.Stage != InvoiceStage.WithFinance)
                    throw new InvoiceRuleException("wrong_stage", "The invoice is still with the coordinator.");
                return;
            default:
                throw new AuthException("forbidden", "You cannot record payments.", 403);
        }
    }

    private static void EnsureCoordinatorOrOwner(InspectionRequest? job, Guid actorId, string role)
    {
        if (role == RoleNames.Admin) return;
        if (role != RoleNames.Coordinator || job is null || !RequestAccess.CanManageQuotes(job, actorId, role))
            throw new AuthException("forbidden", "Only the coordinator that owns the request can do this.", 403);
    }

    private static void EnsureDelivered(InspectionRequest? job)
    {
        if (job?.Status != JobStatus.SentToClient)
            throw new InvoiceRuleException("not_delivered", "The certificates have not been delivered to the client yet.");
    }

    private async Task<(Invoice Invoice, InspectionRequest? Request)> LoadAsync(
        Guid id, Guid actorId, string role, bool tracking, CancellationToken cancellationToken)
    {
        var invoice = await invoices.GetByIdAsync(id, tracking, cancellationToken) ?? throw new KeyNotFoundException("Invoice was not found.");
        var request = await requests.GetByIdAsync(invoice.RequestId, false, cancellationToken);
        // A coordinator only sees the invoices of the requests they may see; the others are as good as missing.
        if (role == RoleNames.Coordinator && (request is null || !RequestAccess.IsVisible(request, actorId, role)))
            throw new KeyNotFoundException("Invoice was not found.");
        return (invoice, request);
    }

    // ---- answers ----

    private async Task<InvoiceDetail> DetailAsync(Invoice invoice, InspectionRequest? request, Guid actorId, string role, CancellationToken cancellationToken)
    {
        var ids = invoice.Payments.Select(item => item.RecordedBy)
            .Concat(invoice.Events.Where(item => item.ActorId is not null).Select(item => item.ActorId!.Value))
            .Distinct().ToArray();
        var names = ids.Length == 0 ? new Dictionary<Guid, string>() : await users.GetFullNamesAsync(ids, cancellationToken);
        string? NameOf(Guid? userId) => userId is Guid value && names.TryGetValue(value, out var name) ? name : null;

        var payments = invoice.Payments.OrderBy(item => item.RecordedAt).Select(item => new InvoicePaymentRow(
            item.Id, item.AmountPiastres, item.PaidOn, item.Label, item.ReversesPaymentId, item.Reason, item.RecordedBy, NameOf(item.RecordedBy), item.RecordedAt)).ToList();
        var history = invoice.Events.Select(item => new InvoiceHistoryRow(item.At, KindOf(item.Kind), item.ActorId, NameOf(item.ActorId), item.Note, null))
            .Concat(invoice.Payments.Select(item => new InvoiceHistoryRow(
                item.RecordedAt, item.IsReversal ? "payment_reversed" : "payment", item.RecordedBy, NameOf(item.RecordedBy),
                item.IsReversal ? item.Reason : item.Label, item.AmountPiastres)))
            .OrderBy(item => item.At).ToList();
        var lines = invoice.GetItems().Select(item => new InvoiceLine(
            item.Type, item.Description, item.Quantity, item.UnitPricePiastres, item.PricingUnit, item.TotalPiastres)).ToList();

        return new InvoiceDetail(
            Summary(invoice), invoice.SubtotalPiastres, invoice.DiscountPercent, invoice.DiscountPiastres, invoice.VatPercent, invoice.VatPiastres,
            lines, payments, history, ActionsFor(invoice, request, actorId, role));
    }

    private static IReadOnlyList<string> ActionsFor(Invoice invoice, InspectionRequest? request, Guid actorId, string role)
    {
        var actions = new List<string>();
        var closed = invoice.Stage == InvoiceStage.Closed;
        var admin = role == RoleNames.Admin;
        var owner = role == RoleNames.Coordinator && request is not null && RequestAccess.CanManageQuotes(request, actorId, role);
        var finance = role == RoleNames.FinancialManager && invoice.Stage == InvoiceStage.WithFinance;
        var hasPayments = invoice.Payments.Any(item => item.AmountPiastres > 0);

        if (!closed && (admin || owner && invoice.Stage == InvoiceStage.Open || finance)) actions.Add("record_payment");
        if (hasPayments && (admin || !closed && (owner && invoice.Stage == InvoiceStage.Open || finance))) actions.Add("reverse_payment");
        var delivered = request?.Status == JobStatus.SentToClient;
        if (!closed && (admin || owner) && delivered)
        {
            actions.Add("email_client");
            if (invoice.Stage == InvoiceStage.Open) actions.Add("hand_over");
        }

        if (!closed && (admin || finance))
        {
            if (invoice.Stage == InvoiceStage.WithFinance && invoice.BalancePiastres == 0) actions.Add("close");
            actions.Add("close_manually");
        }

        return actions;
    }

    internal static InvoiceSummary Summary(Invoice invoice) => new(
        invoice.Id, invoice.Number, invoice.ClientName, invoice.ClientId, invoice.QuotationReference, invoice.RequestId,
        invoice.TotalPiastres, invoice.PaidPiastres, invoice.BalancePiastres, invoice.PaymentState, StageOf(invoice.Stage),
        invoice.CreatedAt, invoice.ClientEmailedAt, invoice.HandedOverAt, invoice.ClosedAt);

    internal static string StageOf(InvoiceStage stage) => stage switch
    {
        InvoiceStage.Open => "open",
        InvoiceStage.WithFinance => "with_finance",
        _ => "closed"
    };

    private static string KindOf(InvoiceEventKind kind) => kind switch
    {
        InvoiceEventKind.Created => "created",
        InvoiceEventKind.Emailed => "emailed",
        InvoiceEventKind.HandedOver => "handed_over",
        _ => "closed"
    };

    private static InvoiceStage? ParseStage(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : value.Trim().ToLowerInvariant() switch
        {
            "open" => InvoiceStage.Open,
            "with_finance" => InvoiceStage.WithFinance,
            "closed" => InvoiceStage.Closed,
            _ => throw new ArgumentException("stage must be 'open', 'with_finance' or 'closed'.")
        };
}
