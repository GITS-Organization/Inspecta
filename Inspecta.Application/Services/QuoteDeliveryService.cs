using System.Globalization;
using System.Text;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class QuoteDeliveryService(
    IRequestRepository requestRepository,
    ICoordinatorEmailConfigRepository configRepository,
    IUserRepository userRepository,
    IOutboundEmailRepository outboundEmailRepository,
    ISecretProtector secretProtector,
    IPdfGenerator pdfGenerator,
    IEmailSender emailSender) : IQuoteDeliveryService
{
    public async Task<OutboundEmail?> DeliverAsync(
        InspectionRequest request,
        Quote quote,
        OutboundEmailKind kind,
        CancellationToken cancellationToken = default)
    {
        var source = await requestRepository.GetEmailAsync(request.Id, cancellationToken);
        if (source is null) return null;

        var recipient = ResolveRecipient(source)
            ?? throw new ArgumentException("The original email has no address to reply to.");
        if (request.CoordinatorId is not Guid coordinatorId || request.ManagerId is not Guid managerId)
            throw new InvalidOperationException("The request has no coordinator to send the email from.");

        var existing = await outboundEmailRepository.GetAsync(quote.Id, kind, cancellationToken);
        // Already delivered (a previous call sent it but did not finish saving the quote): never send twice.
        if (existing is { Status: OutboundEmailStatus.Sent }) return existing;
        if (existing is { Status: OutboundEmailStatus.Pending, IsStalePending: false })
            throw new InvalidOperationException("This email is already being sent.");

        var config = await configRepository.GetByCoordinatorAsync(coordinatorId, cancellationToken)
            ?? throw new ArgumentException(
                "The coordinator mailbox is not configured, so the quotation cannot be emailed. Configure the mailbox first.");
        var coordinator = await userRepository.GetByIdAsync(coordinatorId, cancellationToken)
            ?? throw new KeyNotFoundException("Coordinator was not found.");
        var fromAddress = ResolveFromAddress(config, coordinator);

        var pdf = pdfGenerator.Generate(QuotationPdfModelFactory.Create(quote, request, DateTimeOffset.UtcNow));
        var row = existing;
        if (row is null)
            row = OutboundEmail.Create(managerId, request.Id, quote.Id, kind, DomainOf(fromAddress));
        row.BeginAttempt(
            recipient.Address,
            recipient.Name,
            fromAddress,
            coordinator.FullName,
            ComposeSubject(source.Subject, quote),
            ComposeBody(kind, quote, recipient.Name, coordinator.FullName, source.Subject),
            source.MessageId,
            string.Join(' ', new[] { source.References, source.MessageId }.Where(value => !string.IsNullOrWhiteSpace(value))),
            pdf.FileName,
            pdf.Content);

        if (existing is null) await outboundEmailRepository.AddAsync(row, cancellationToken);
        else await outboundEmailRepository.SaveAsync(cancellationToken);

        var smtp = config.ResolveSmtp();
        try
        {
            await emailSender.SendAsync(
                new SmtpSettings(smtp.Server, smtp.Port, smtp.UseSsl, config.EmailUsername, secretProtector.Unprotect(config.EncryptedPassword)),
                new OutgoingEmail(
                    row.FromAddress, row.FromName, row.ToAddress, row.ToName, row.Subject, row.BodyText, row.MessageId,
                    row.InReplyTo, row.References, row.AttachmentFileName, row.AttachmentContent),
                cancellationToken);
        }
        catch (Exception exception)
        {
            row.MarkFailed(exception.Message);
            await outboundEmailRepository.SaveAsync(CancellationToken.None);
            throw exception as EmailDeliveryException ?? new EmailDeliveryException("The email could not be sent.", exception);
        }

        // Recorded straight away: if saving the quote fails afterwards, a retry finds the email already sent.
        row.MarkSent();
        await outboundEmailRepository.SaveAsync(CancellationToken.None);
        return row;
    }

    // The sender asked to be answered at Reply-To when they set one; otherwise the From address.
    private static EmailAddressInfo? ResolveRecipient(RequestEmail source)
    {
        var replyTo = source.GetReplyToAddresses().FirstOrDefault(address => !string.IsNullOrWhiteSpace(address.Address));
        if (replyTo is not null) return replyTo;
        return string.IsNullOrWhiteSpace(source.FromAddress) ? null : new EmailAddressInfo(source.FromAddress, source.FromName);
    }

    private static string ResolveFromAddress(CoordinatorEmailConfig config, AppUser coordinator) =>
        config.EmailUsername.Contains('@', StringComparison.Ordinal) ? config.EmailUsername : coordinator.Email;

    private static string DomainOf(string address)
    {
        var at = address.LastIndexOf('@');
        return at >= 0 && at < address.Length - 1 ? address[(at + 1)..] : "inspecta.local";
    }

    private static string ComposeSubject(string? originalSubject, Quote quote)
    {
        var subject = originalSubject is null ? string.Empty : string.Concat(originalSubject.Where(character => !char.IsControl(character))).Trim();
        if (subject.Length == 0) return $"Quotation {quote.ProposalNumber}";
        return subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) ? subject : "Re: " + subject;
    }

    private static string ComposeBody(
        OutboundEmailKind kind,
        Quote quote,
        string? recipientName,
        string coordinatorName,
        string? originalSubject)
    {
        var body = new StringBuilder();
        body.AppendLine($"Dear {(string.IsNullOrWhiteSpace(recipientName) ? "Sir/Madam" : recipientName)},");
        body.AppendLine();
        var regarding = string.IsNullOrWhiteSpace(originalSubject) ? string.Empty : $" regarding \"{originalSubject.Trim()}\"";
        body.AppendLine(kind == OutboundEmailKind.Accept
            ? $"We are pleased to confirm that we accept your request{regarding}. Please find attached our quotation {quote.ProposalNumber} for:"
            : $"Thank you for your enquiry{regarding}. Please find attached our technical and commercial offer {quote.ProposalNumber} for:");
        body.AppendLine();
        foreach (var item in quote.GetItems())
            body.AppendLine($"- {item.Description ?? item.Type} x {item.Quantity.ToString(CultureInfo.InvariantCulture)}");
        body.AppendLine();
        body.AppendLine($"Total (excluding VAT): {(quote.ItemsTotalPiastres / 100m).ToString("N2", CultureInfo.InvariantCulture)} EGP");
        body.AppendLine($"Valid for {quote.ValidForDays.ToString(CultureInfo.InvariantCulture)} days from the date of this email.");
        if (!string.IsNullOrWhiteSpace(quote.Message))
        {
            body.AppendLine();
            body.AppendLine(quote.Message);
        }

        body.AppendLine();
        body.AppendLine("Kind regards,");
        body.AppendLine(coordinatorName);
        return body.ToString();
    }
}
