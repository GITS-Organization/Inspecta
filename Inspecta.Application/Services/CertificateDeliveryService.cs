using System.Globalization;
using System.IO.Compression;
using System.Text;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

/// <summary>Turns the emailing of certificates on or off (<c>CertificateDelivery:Enabled</c>). Off unless configured otherwise.</summary>
public sealed record CertificateDeliveryOptions(bool Enabled = false);

/// <summary>Who the certificates would be emailed to, and what the email would say.</summary>
public sealed record CertificateDeliveryPlan(
    string? ToAddress,
    string? ToName,
    string Subject,
    string Body,
    IReadOnlyList<string> AttachmentNames,
    bool RepliesToOriginalEmail);

public sealed record CertificateDeliveryResult(bool EmailSent, string? SentTo, string? Notice);

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

    public bool Enabled => options is { Enabled: true };

    public async Task<CertificateDeliveryPlan> PlanAsync(
        InspectionRequest request,
        IReadOnlyList<IssuedCertificateFile> files,
        string? recipientOverride,
        string? message,
        CancellationToken cancellationToken = default)
    {
        var source = await requestRepository.GetEmailAsync(request.Id, cancellationToken);
        var replyTo = source is null ? null : QuoteDeliveryService.ResolveRecipient(source);
        var client = request.ClientId is Guid clientId ? await clientRepository.GetByIdAsync(clientId, tracking: false, cancellationToken) : null;

        string? toAddress = null, toName = null;
        var replies = false;
        if (!string.IsNullOrWhiteSpace(recipientOverride)) toAddress = recipientOverride.Trim();
        else if (replyTo is not null)
        {
            (toAddress, toName, replies) = (replyTo.Address, replyTo.Name, true);
        }
        else if (!string.IsNullOrWhiteSpace(client?.CertificatesEmail)) toAddress = client.CertificatesEmail;

        var coordinator = request.CoordinatorId is Guid coordinatorId ? await userRepository.GetByIdAsync(coordinatorId, cancellationToken) : null;
        var body = string.IsNullOrWhiteSpace(message)
            ? ComposeBody(toName, request.ClientName, files, coordinator?.FullName)
            : message.Trim();
        return new CertificateDeliveryPlan(
            toAddress,
            toName,
            ComposeSubject(source?.Subject, request.ClientName),
            body,
            AttachmentNames(files),
            replies && string.IsNullOrWhiteSpace(recipientOverride));
    }

    public async Task<CertificateDeliveryResult> DeliverAsync(
        InspectionRequest request,
        IReadOnlyList<IssuedCertificateFile> files,
        string? recipientOverride,
        string? message,
        CancellationToken cancellationToken = default)
    {
        if (!Enabled)
            return new CertificateDeliveryResult(false, null, "Email delivery is switched off, so no email was sent. The certificates are marked as handed over.");

        var plan = await PlanAsync(request, files, recipientOverride, message, cancellationToken);
        if (plan.ToAddress is null)
            return new CertificateDeliveryResult(false, null, "There is no address to email: the request has no source email and the client has no certificates email. The certificates are marked as handed over.");

        // Already emailed by an earlier call that did not finish saving: never send twice.
        if (await deliveryRepository.GetSentAsync(request.Id, cancellationToken) is { } earlier)
            return new CertificateDeliveryResult(true, earlier.ToAddress, "The certificates were already emailed.");

        if (request.CoordinatorId is not Guid coordinatorId || request.ManagerId is not Guid managerId)
            throw new InvalidOperationException("The request has no coordinator to send the email from.");
        var config = await configRepository.GetByCoordinatorAsync(coordinatorId, cancellationToken)
            ?? throw new ArgumentException("The coordinator mailbox is not configured, so the certificates cannot be emailed. Configure the mailbox first.");
        var coordinator = await userRepository.GetByIdAsync(coordinatorId, cancellationToken)
            ?? throw new KeyNotFoundException("Coordinator was not found.");
        var fromAddress = QuoteDeliveryService.ResolveFromAddress(config, coordinator);
        var source = plan.RepliesToOriginalEmail ? await requestRepository.GetEmailAsync(request.Id, cancellationToken) : null;

        var attachments = BuildAttachments(files);
        var row = await deliveryRepository.GetPendingAsync(request.Id, cancellationToken);
        var isNew = row is null;
        row ??= CertificateDelivery.Create(
            managerId,
            request.Id,
            plan.ToAddress,
            plan.Subject,
            plan.Body,
            $"<{Guid.NewGuid():N}@{QuoteDeliveryService.DomainOf(fromAddress)}>",
            attachments.Select(attachment => attachment.FileName));
        row.MarkAttempt();
        if (isNew) await deliveryRepository.AddAsync(row, cancellationToken);
        else await deliveryRepository.SaveAsync(cancellationToken);

        var smtp = config.ResolveSmtp();
        try
        {
            await emailSender.SendAsync(
                new SmtpSettings(smtp.Server, smtp.Port, smtp.UseSsl, config.EmailUsername, secretProtector.Unprotect(config.EncryptedPassword)),
                new OutgoingEmail(
                    fromAddress,
                    coordinator.FullName,
                    plan.ToAddress,
                    plan.ToName,
                    plan.Subject,
                    plan.Body,
                    row.MessageId,
                    source?.MessageId,
                    source is null ? null : string.Join(' ', new[] { source.References, source.MessageId }.Where(value => !string.IsNullOrWhiteSpace(value))),
                    attachments[0].FileName,
                    attachments[0].Content,
                    attachments.Skip(1).ToArray()),
                cancellationToken);
        }
        catch (Exception exception)
        {
            row.MarkFailed(exception.Message);
            await deliveryRepository.SaveAsync(CancellationToken.None);
            throw exception as EmailDeliveryException ?? new EmailDeliveryException("The email could not be sent.", exception);
        }

        row.MarkSent();
        await deliveryRepository.SaveAsync(CancellationToken.None);
        return new CertificateDeliveryResult(true, plan.ToAddress, null);
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

    private static string ComposeSubject(string? originalSubject, string clientName)
    {
        var subject = originalSubject is null ? string.Empty : string.Concat(originalSubject.Where(character => !char.IsControl(character))).Trim();
        if (subject.Length == 0) return $"Inspection certificates - {clientName}";
        return subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) ? subject : "Re: " + subject;
    }

    private static string ComposeBody(string? recipientName, string clientName, IReadOnlyList<IssuedCertificateFile> files, string? coordinatorName)
    {
        var body = new StringBuilder();
        body.AppendLine($"Dear {(string.IsNullOrWhiteSpace(recipientName) ? "Sir/Madam" : recipientName)},");
        body.AppendLine();
        body.AppendLine($"Please find attached the certificates of thorough examination for {clientName}:");
        body.AppendLine();
        foreach (var file in files)
            body.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- {file.ItemName} ({file.CertificateNumber})"));
        body.AppendLine();
        body.AppendLine("Each certificate carries a QR code: scan it to confirm that the certificate is genuine.");
        body.AppendLine();
        body.AppendLine("Kind regards,");
        body.AppendLine(string.IsNullOrWhiteSpace(coordinatorName) ? "Integrity International Inspection" : coordinatorName);
        return body.ToString();
    }
}
