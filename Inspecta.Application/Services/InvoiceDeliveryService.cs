using System.Globalization;
using System.Text;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

public sealed class InvoiceDeliveryService(
    IRequestRepository requestRepository,
    ICoordinatorEmailConfigRepository configRepository,
    IUserRepository userRepository,
    ISecretProtector secretProtector,
    IPdfGenerator pdfGenerator,
    IEmailSender emailSender,
    IClientRepository? clientRepository = null,
    QuoteDeliveryOptions? options = null,
    IEmailBranding? branding = null,
    ICompanyBrandingProvider? companyBranding = null,
    IMailboxAuthenticator? mailboxAuthenticator = null) : IInvoiceDeliveryService
{
    public async Task<QuoteEmailPlan> PlanAsync(Invoice invoice, InspectionRequest request, QuoteEmailDraft? draft, CancellationToken cancellationToken = default)
    {
        var source = await requestRepository.GetEmailAsync(request.Id, cancellationToken);
        var client = await ClientOfAsync(invoice, cancellationToken);
        var (toAddress, toName, replies) = ResolveTarget(source, client, draft);
        var copies = CertificateDeliveryService.NormalizeCopies(draft?.Cc, toAddress);
        var coordinator = request.CoordinatorId is Guid coordinatorId ? await userRepository.GetByIdAsync(coordinatorId, cancellationToken) : null;
        var config = request.CoordinatorId is Guid configOwner ? await configRepository.GetByCoordinatorAsync(configOwner, cancellationToken) : null;

        var subject = string.IsNullOrWhiteSpace(draft?.Subject)
            ? $"Invoice {invoice.Number} - {invoice.ClientName}"
            : CertificateDeliveryService.CleanSubject(draft.Subject);
        var body = string.IsNullOrWhiteSpace(draft?.Body) ? ComposeBody(invoice, coordinator?.FullName) : draft.Body.Trim();
        if (body.Length > CertificateDeliveryService.MaxBodyLength)
            throw new ArgumentException($"The email text is longer than {CertificateDeliveryService.MaxBodyLength} characters.");
        var fromAddress = config is null || coordinator is null ? null : QuoteDeliveryService.ResolveFromAddress(config, coordinator);
        var company = companyBranding is null ? null : await companyBranding.GetAsync(request.ManagerId, cancellationToken);
        var pdfName = pdfGenerator.Generate(InvoicePdfModelFactory.Create(invoice, client, company, InvoiceCalendar.Today())).FileName;

        var blockers = new List<string>();
        if (options is { Enabled: false }) blockers.Add("Emailing is switched off.");
        if (toAddress is null) blockers.Add("There is no address to send to: the request did not come from an email and the client has no certificates email. Type one in the To field.");
        if (config is null) blockers.Add("The coordinator mailbox is not configured.");

        var preview = EmailLayout.ForCoordinator(
            body, coordinator, fromAddress, source, EmailLayout.BrandingFor(company, branding), EmailLayout.PreviewLogoFor(request.ManagerId, company));
        return new QuoteEmailPlan(
            options is not { Enabled: false }, toAddress, toName, copies, subject, body, pdfName, fromAddress, coordinator?.FullName, replies, blockers, preview.Html,
            draft?.Extra is { Count: > 0 } extra ? extra.Select(file => file.FileName).ToArray() : null);
    }

    public async Task<QuoteEmailPlan> SendAsync(Invoice invoice, InspectionRequest request, QuoteEmailDraft? draft, CancellationToken cancellationToken = default)
    {
        if (options is { Enabled: false })
            throw new AuthException("email_disabled", "Emailing is switched off, so the invoice cannot be sent.", 409);
        if (request.CoordinatorId is not Guid coordinatorId)
            throw new InvalidOperationException("The request has no coordinator to send the email from.");

        var plan = await PlanAsync(invoice, request, draft, cancellationToken);
        if (plan.ToAddress is null) throw new ArgumentException("There is no address to send the invoice to. Type one in the To field.");
        var config = await configRepository.GetByCoordinatorAsync(coordinatorId, cancellationToken)
            ?? throw new ArgumentException("The coordinator mailbox is not configured, so the invoice cannot be emailed. Configure the mailbox first.");
        var coordinator = await userRepository.GetByIdAsync(coordinatorId, cancellationToken)
            ?? throw new KeyNotFoundException("Coordinator was not found.");
        var fromAddress = QuoteDeliveryService.ResolveFromAddress(config, coordinator);
        // Resolved before anything is sent: a mailbox that must be connected again stops here with a clear 409.
        var smtp = await QuoteDeliveryService.SmtpSettingsAsync(config, mailboxAuthenticator, secretProtector, cancellationToken);

        var source = await requestRepository.GetEmailAsync(request.Id, cancellationToken);
        var client = await ClientOfAsync(invoice, cancellationToken);
        var company = companyBranding is null ? null : await companyBranding.GetAsync(request.ManagerId, cancellationToken);
        var pdf = pdfGenerator.Generate(InvoicePdfModelFactory.Create(invoice, client, company, InvoiceCalendar.Today()));
        var laidOut = EmailLayout.ForCoordinator(plan.Body, coordinator, fromAddress, source, EmailLayout.BrandingFor(company, branding));
        try
        {
            await emailSender.SendAsync(
                smtp,
                new OutgoingEmail(
                    fromAddress, coordinator.FullName, plan.ToAddress, plan.ToName, plan.Subject, laidOut.Text,
                    $"<{Guid.NewGuid():N}@{QuoteDeliveryService.DomainOf(fromAddress)}>",
                    source?.MessageId,
                    source is null ? null : string.Join(' ', new[] { source.References, source.MessageId }.Where(value => !string.IsNullOrWhiteSpace(value))),
                    pdf.FileName,
                    pdf.Content,
                    MoreAttachments: draft?.Extra is { Count: > 0 } ? draft.Extra : null,
                    Cc: plan.Cc.Count == 0 ? null : plan.Cc,
                    BodyHtml: laidOut.Html,
                    InlineImages: laidOut.InlineImages),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw exception as EmailDeliveryException ?? new EmailDeliveryException("The email could not be sent.", exception);
        }

        return plan;
    }

    private async Task<Client?> ClientOfAsync(Invoice invoice, CancellationToken cancellationToken) =>
        clientRepository is not null && invoice.ClientId is Guid clientId ? await clientRepository.GetByIdAsync(clientId, false, cancellationToken) : null;

    // The address typed by the coordinator wins; then the sender of the original email (the mail stays in its thread); then the client's certificates email.
    private static (string? Address, string? Name, bool RepliesToOriginal) ResolveTarget(RequestEmail? source, Client? client, QuoteEmailDraft? draft)
    {
        var original = source is null ? null : QuoteDeliveryService.ResolveRecipient(source);
        if (!string.IsNullOrWhiteSpace(draft?.To))
        {
            var typed = CertificateDeliveryService.NormalizeAddress(draft.To, "To");
            var sameAsOriginal = original is not null && string.Equals(original.Address, typed, StringComparison.OrdinalIgnoreCase);
            return (typed, sameAsOriginal ? original!.Name : null, source is not null);
        }

        if (original is not null) return (original.Address, original.Name, true);
        return string.IsNullOrWhiteSpace(client?.CertificatesEmail) ? (null, null, false) : (client.CertificatesEmail.Trim(), client.Name, false);
    }

    private static string ComposeBody(Invoice invoice, string? coordinatorName)
    {
        string Egp(long piastres) => $"{(piastres / 100m).ToString("N2", CultureInfo.InvariantCulture)} EGP";
        var body = new StringBuilder();
        body.AppendLine("Dears,");
        body.AppendLine("good day,");
        body.AppendLine();
        body.AppendLine($"Please find attached invoice {invoice.Number}" +
            (string.IsNullOrWhiteSpace(invoice.QuotationReference) ? "." : $" for quotation {invoice.QuotationReference}."));
        body.AppendLine();
        body.AppendLine($"Total: {Egp(invoice.TotalPiastres)}");
        if (invoice.PaidPiastres > 0)
        {
            body.AppendLine($"Paid so far: {Egp(invoice.PaidPiastres)}");
            body.AppendLine($"Balance: {Egp(invoice.BalancePiastres)}");
        }

        body.AppendLine();
        body.AppendLine("Regards,");
        _ = coordinatorName;
        return body.ToString();
    }
}
