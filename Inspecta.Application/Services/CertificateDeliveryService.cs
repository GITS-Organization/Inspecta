using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

/// <summary>Turns the emailing of certificates on or off (<c>CertificateDelivery:Enabled</c>). Off unless configured otherwise.</summary>
public sealed record CertificateDeliveryOptions(bool Enabled = false);

/// <summary>What the coordinator may change before the email goes out; anything left out keeps the suggested value.</summary>
public sealed record CertificateEmailDraft(
    string? To = null,
    IReadOnlyList<string>? Cc = null,
    string? Subject = null,
    string? Body = null);

/// <summary>Who the certificates would be emailed to, and what the email would say.</summary>
public sealed record CertificateDeliveryPlan(
    string? ToAddress,
    string? ToName,
    IReadOnlyList<string> Cc,
    string Subject,
    string Body,
    IReadOnlyList<string> AttachmentNames,
    bool RepliesToOriginalEmail,
    string? FromAddress,
    string? FromName,
    bool MailboxConfigured);

public sealed record CertificateDeliveryResult(
    bool EmailSent,
    string? SentTo,
    string? Notice,
    DateTimeOffset? SentAt = null,
    IReadOnlyList<string>? Cc = null,
    string? Subject = null);

/// <summary>A test copy of the email, sent to the person who asked for it and recorded nowhere.</summary>
public sealed record CertificateTestResult(string SentTo, string Subject);

/// <summary>
/// Emails the approved certificates of a job to the client: as a reply to the email the request came in on, from the
/// coordinator's mailbox, with every PDF attached. While <c>CertificateDelivery:Enabled</c> is off nothing is sent and
/// nothing fails: the job is simply handed over without an email.
/// </summary>
public sealed class CertificateDeliveryService(
    IRequestRepository requestRepository,
    IClientRepository clientRepository,
    ICoordinatorEmailConfigRepository configRepository,
    IUserRepository userRepository,
    ICertificateDeliveryRepository deliveryRepository,
    ISecretProtector secretProtector,
    IEmailSender emailSender,
    CertificateDeliveryOptions? options = null)
{
    // Above this many bytes the PDFs travel as one ZIP: mail servers refuse very large messages.
    private const int MaxAttachedBytes = 18 * 1024 * 1024;
    private const int MaxCopies = 10;
    private const int MaxSubjectLength = 300;
    private const int MaxBodyLength = 20000;

    public bool Enabled => options is { Enabled: true };

    public async Task<CertificateDeliveryPlan> PlanAsync(
        InspectionRequest request,
        IReadOnlyList<IssuedCertificateFile> files,
        CertificateEmailDraft? draft,
        CancellationToken cancellationToken = default)
    {
        var source = await requestRepository.GetEmailAsync(request.Id, cancellationToken);
        var replyTo = source is null ? null : QuoteDeliveryService.ResolveRecipient(source);
        var client = request.ClientId is Guid clientId ? await clientRepository.GetByIdAsync(clientId, tracking: false, cancellationToken) : null;

        string? toAddress = null, toName = null;
        var replies = false;
        if (!string.IsNullOrWhiteSpace(draft?.To)) toAddress = NormalizeAddress(draft.To, "To");
        else if (replyTo is not null)
        {
            (toAddress, toName, replies) = (replyTo.Address, replyTo.Name, true);
        }
        else if (!string.IsNullOrWhiteSpace(client?.CertificatesEmail)) toAddress = client.CertificatesEmail;

        var copies = NormalizeCopies(draft?.Cc, toAddress);
        var coordinator = request.CoordinatorId is Guid coordinatorId ? await userRepository.GetByIdAsync(coordinatorId, cancellationToken) : null;
        var config = request.CoordinatorId is Guid configOwner ? await configRepository.GetByCoordinatorAsync(configOwner, cancellationToken) : null;

        var subject = string.IsNullOrWhiteSpace(draft?.Subject)
            ? ComposeSubject(source?.Subject, request.ClientName)
            : CleanSubject(draft.Subject);
        var body = string.IsNullOrWhiteSpace(draft?.Body)
            ? ComposeBody(toName, request.ClientName, files, coordinator?.FullName)
            : draft.Body.Trim();
        if (body.Length > MaxBodyLength) throw new ArgumentException($"The email text is longer than {MaxBodyLength} characters.");

        return new CertificateDeliveryPlan(
            toAddress,
            toName,
            copies,
            subject,
            body,
            AttachmentNames(files),
            replies && string.IsNullOrWhiteSpace(draft?.To),
            config is not null && coordinator is not null ? QuoteDeliveryService.ResolveFromAddress(config, coordinator) : null,
            coordinator?.FullName,
            config is not null);
    }

    public async Task<CertificateDeliveryResult> DeliverAsync(
        InspectionRequest request,
        IReadOnlyList<IssuedCertificateFile> files,
        CertificateEmailDraft? draft,
        CancellationToken cancellationToken = default)
    {
        if (!Enabled)
            return new CertificateDeliveryResult(false, null, "Email delivery is switched off, so no email was sent. The certificates are marked as handed over.");

        var plan = await PlanAsync(request, files, draft, cancellationToken);
        if (plan.ToAddress is null)
            throw new AuthException(
                "no_recipient",
                "There is no address to email: the request has no source email and the client has no certificates email. Type an address, or use the manual delivery if the certificates were handed over another way.",
                409);

        // Already emailed by an earlier call that did not finish saving: never send twice.
        if (await deliveryRepository.GetSentAsync(request.Id, cancellationToken) is { } earlier)
            return new CertificateDeliveryResult(true, earlier.ToAddress, "The certificates were already emailed.", earlier.SentAt, earlier.Cc, earlier.Subject);

        if (request.CoordinatorId is not Guid coordinatorId || request.ManagerId is not Guid managerId)
            throw new InvalidOperationException("The request has no coordinator to send the email from.");
        var (config, coordinator, fromAddress) = await MailboxAsync(coordinatorId, cancellationToken);
        var source = plan.RepliesToOriginalEmail ? await requestRepository.GetEmailAsync(request.Id, cancellationToken) : null;

        var attachments = BuildAttachments(files);
        var row = await deliveryRepository.GetPendingAsync(request.Id, cancellationToken);
        var isNew = row is null;
        if (row is null)
            row = CertificateDelivery.Create(
                managerId,
                request.Id,
                plan.ToAddress,
                plan.Subject,
                plan.Body,
                $"<{Guid.NewGuid():N}@{QuoteDeliveryService.DomainOf(fromAddress)}>",
                attachments.Select(attachment => attachment.FileName),
                plan.Cc);
        else row.Revise(plan.ToAddress, plan.Cc, plan.Subject, plan.Body, attachments.Select(attachment => attachment.FileName));
        row.MarkAttempt();
        if (isNew) await deliveryRepository.AddAsync(row, cancellationToken);
        else await deliveryRepository.SaveAsync(cancellationToken);

        try
        {
            await SendAsync(config, coordinator, fromAddress, plan.ToAddress, plan.ToName, plan.Cc, plan.Subject, plan.Body, row.MessageId, source, attachments, cancellationToken);
        }
        catch (Exception exception)
        {
            row.MarkFailed(exception.Message);
            await deliveryRepository.SaveAsync(CancellationToken.None);
            throw exception as EmailDeliveryException ?? new EmailDeliveryException("The email could not be sent.", exception);
        }

        row.MarkSent();
        await deliveryRepository.SaveAsync(CancellationToken.None);
        return new CertificateDeliveryResult(true, plan.ToAddress, null, row.SentAt, plan.Cc, plan.Subject);
    }

    /// <summary>
    /// Sends the email exactly as it would go out (with the coordinator's edits and every PDF) to one address, marked [TEST],
    /// so the coordinator sees what the client will get. Nothing is recorded and the job does not change.
    /// </summary>
    public async Task<CertificateTestResult> SendTestAsync(
        InspectionRequest request,
        IReadOnlyList<IssuedCertificateFile> files,
        CertificateEmailDraft? draft,
        string toAddress,
        string? toName,
        CancellationToken cancellationToken = default)
    {
        if (!Enabled)
            throw new AuthException("email_disabled", "Email delivery is switched off, so no test email can be sent.", 409);
        if (request.CoordinatorId is not Guid coordinatorId)
            throw new InvalidOperationException("The request has no coordinator to send the email from.");

        var plan = await PlanAsync(request, files, draft, cancellationToken);
        var (config, coordinator, fromAddress) = await MailboxAsync(coordinatorId, cancellationToken);
        var subject = "[TEST] " + plan.Subject;
        var messageId = $"<{Guid.NewGuid():N}@{QuoteDeliveryService.DomainOf(fromAddress)}>";
        try
        {
            await SendAsync(config, coordinator, fromAddress, toAddress, toName, [], subject, plan.Body, messageId, null, BuildAttachments(files), cancellationToken);
        }
        catch (Exception exception)
        {
            throw exception as EmailDeliveryException ?? new EmailDeliveryException("The email could not be sent.", exception);
        }

        return new CertificateTestResult(toAddress, subject);
    }

    /// <summary>The email that carried this job's certificates, when one was sent.</summary>
    public Task<CertificateDelivery?> GetSentAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        deliveryRepository.GetSentAsync(requestId, cancellationToken);

    private async Task<(CoordinatorEmailConfig Config, AppUser Coordinator, string FromAddress)> MailboxAsync(Guid coordinatorId, CancellationToken cancellationToken)
    {
        var config = await configRepository.GetByCoordinatorAsync(coordinatorId, cancellationToken)
            ?? throw new ArgumentException("The coordinator mailbox is not configured, so the certificates cannot be emailed. Configure the mailbox first.");
        var coordinator = await userRepository.GetByIdAsync(coordinatorId, cancellationToken)
            ?? throw new KeyNotFoundException("Coordinator was not found.");
        return (config, coordinator, QuoteDeliveryService.ResolveFromAddress(config, coordinator));
    }

    private async Task SendAsync(
        CoordinatorEmailConfig config,
        AppUser coordinator,
        string fromAddress,
        string toAddress,
        string? toName,
        IReadOnlyList<string> cc,
        string subject,
        string body,
        string messageId,
        RequestEmail? source,
        IReadOnlyList<EmailAttachment> attachments,
        CancellationToken cancellationToken)
    {
        var smtp = config.ResolveSmtp();
        await emailSender.SendAsync(
            new SmtpSettings(smtp.Server, smtp.Port, smtp.UseSsl, config.EmailUsername, secretProtector.Unprotect(config.EncryptedPassword)),
            new OutgoingEmail(
                fromAddress,
                coordinator.FullName,
                toAddress,
                toName,
                subject,
                body,
                messageId,
                source?.MessageId,
                source is null ? null : string.Join(' ', new[] { source.References, source.MessageId }.Where(value => !string.IsNullOrWhiteSpace(value))),
                attachments[0].FileName,
                attachments[0].Content,
                attachments.Skip(1).ToArray(),
                cc,
                PlainTextToHtml(body)),
            cancellationToken);
    }

    /// <summary>All the PDFs of a job in one ZIP.</summary>
    public static byte[] Zip(IEnumerable<IssuedCertificateFile> files, IEnumerable<(string Name, byte[] Content)>? extras = null)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files.Select(file => (file.FileName, file.Content)).Concat(extras ?? []))
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static IReadOnlyList<EmailAttachment> BuildAttachments(IReadOnlyList<IssuedCertificateFile> files)
    {
        if (files.Sum(file => (long)file.Content.Length) <= MaxAttachedBytes)
            return files.Select(file => new EmailAttachment(file.FileName, file.Content, "application/pdf")).ToArray();
        return [new EmailAttachment("certificates.zip", Zip(files), "application/zip")];
    }

    private static IReadOnlyList<string> AttachmentNames(IReadOnlyList<IssuedCertificateFile> files) =>
        files.Sum(file => (long)file.Content.Length) <= MaxAttachedBytes ? files.Select(file => file.FileName).ToArray() : ["certificates.zip"];

    /// <summary>A plain address, checked: no names, no line breaks, one address.</summary>
    internal static string NormalizeAddress(string value, string field)
    {
        var text = value.Trim();
        if (text.Length is 0 or > 320 || text.Any(char.IsControl) || text.Contains(',') || text.Contains(';') ||
            !MailAddress.TryCreate(text, out var parsed) || !string.Equals(parsed.Address, text, StringComparison.OrdinalIgnoreCase) ||
            !parsed.Host.Contains('.'))
            throw new ArgumentException($"'{value.Trim()}' is not a valid email address ({field}).");
        return parsed.Address;
    }

    private static IReadOnlyList<string> NormalizeCopies(IReadOnlyList<string>? cc, string? toAddress)
    {
        var copies = (cc ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeAddress(value, "Cc"))
            .Where(value => !string.Equals(value, toAddress, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (copies.Length > MaxCopies) throw new ArgumentException($"At most {MaxCopies} addresses can be copied.");
        return copies;
    }

    private static string CleanSubject(string subject)
    {
        var cleaned = string.Concat(subject.Where(character => !char.IsControl(character))).Trim();
        return cleaned.Length > MaxSubjectLength ? cleaned[..MaxSubjectLength] : cleaned;
    }

    private static string ComposeSubject(string? originalSubject, string clientName)
    {
        var subject = originalSubject is null ? string.Empty : CleanSubject(originalSubject);
        if (subject.Length == 0) return $"Inspection certificates - {clientName}";
        return subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) ? subject : "Re: " + subject;
    }

    /// <summary>The suggested email: a greeting, one block per certificate with its result, dates and check link, and the sign-off.</summary>
    internal static string ComposeBody(string? recipientName, string clientName, IReadOnlyList<IssuedCertificateFile> files, string? coordinatorName)
    {
        var body = new StringBuilder();
        body.AppendLine($"Dear {(string.IsNullOrWhiteSpace(recipientName) ? "Sir/Madam" : recipientName)},");
        body.AppendLine();
        body.AppendLine($"Please find attached the Reports of Thorough Examination for {clientName}:");
        body.AppendLine();
        var number = 0;
        foreach (var file in files)
        {
            number++;
            var result = file.Result.ToInspectionResult() is { } verdict ? verdict.Label() : null;
            body.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{number}. {file.ItemName} - {file.CertificateNumber}{(result is null ? string.Empty : " - " + result)}"));
            var dates = new List<string>();
            if (file.ExaminationDate is { } examined) dates.Add(string.Create(CultureInfo.InvariantCulture, $"examined {examined:dd/MM/yyyy}"));
            if (file.NextExaminationDate is { } next) dates.Add(string.Create(CultureInfo.InvariantCulture, $"next examination {next:dd/MM/yyyy}"));
            if (dates.Count > 0) body.AppendLine("   " + string.Join(", ", dates));
            if (!string.IsNullOrWhiteSpace(file.QrData)) body.AppendLine("   Check or download: " + file.QrData);
        }

        body.AppendLine();
        body.AppendLine("Each certificate carries a QR code. Scan it, or open the link above, to confirm that the certificate is genuine and to download it again at any time.");
        body.AppendLine();
        body.AppendLine("Kind regards,");
        body.AppendLine(string.IsNullOrWhiteSpace(coordinatorName) ? "Integrity International Inspection" : coordinatorName);
        if (!string.IsNullOrWhiteSpace(coordinatorName)) body.AppendLine("Integrity International Inspection");
        return body.ToString().TrimEnd() + Environment.NewLine;
    }

    // The HTML version of the plain text: the same words and line breaks, with the links made clickable.
    private static string PlainTextToHtml(string text)
    {
        var encoded = WebUtility.HtmlEncode(text);
        var linked = Regex.Replace(encoded, @"https?://[^\s<]+", match => $"<a href=\"{match.Value}\">{match.Value}</a>");
        return "<div style=\"font-family:'Segoe UI',Arial,sans-serif;font-size:14px;line-height:1.5;white-space:pre-wrap\">" + linked + "</div>";
    }
}
