using System.Net;
using System.Text.RegularExpressions;
using Inspecta.Application.Contracts.Renewals;
using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

/// <summary>
/// The machines that are due, grouped by client, for the coordinator to remind; and the reminder itself: an email from the coordinator's own mailbox, sent only
/// when asked, once per key. Days are counted in the Cairo calendar, as on the executive screens.
/// </summary>
public sealed class RenewalsService(
    IRenewalsRepository repository,
    ICoordinatorEmailConfigRepository configRepository,
    IUserRepository userRepository,
    ISecretProtector secretProtector,
    IEmailSender emailSender,
    QuoteDeliveryOptions? options = null,
    IEmailBranding? branding = null,
    ICompanyBrandingProvider? companyBranding = null,
    IMailboxAuthenticator? mailboxAuthenticator = null,
    TimeProvider? clock = null,
    IInspectionRequestService? inspectionRequests = null,
    ITransactionRunner? transactions = null) : IRenewalsService
{
    /// <summary>The longest window: machines due later than this are not listed.</summary>
    private const int LongestWindowDays = 90;
    private const int MaxPageSize = 100;

    public async Task<RenewalsListResponse> ListAsync(
        Guid managerId, string? window, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var windowDays = ParseWindow(window);
        page = Math.Max(page, 1);
        pageSize = pageSize is < 1 ? 50 : Math.Min(pageSize, MaxPageSize);
        var today = InvoiceCalendar.Today((clock ?? TimeProvider.System).GetUtcNow());

        // One day past the longest window, in UTC, so no machine of the Cairo calendar is missed; the exact days are counted below.
        var dueBefore = new DateTimeOffset(today.AddDays(LongestWindowDays + 2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var rows = await repository.ListDueAsync(managerId, dueBefore, cancellationToken);

        var clients = rows
            .Select(row => (Row: row, Machines: row.Machines
                .Select(machine => (Machine: machine, Days: InvoiceCalendar.Of(machine.NextDueAt).DayNumber - today.DayNumber))
                .Where(item => item.Days <= LongestWindowDays)
                .OrderBy(item => item.Days).ThenBy(item => item.Machine.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()))
            .Where(item => item.Machines.Count > 0)
            .ToList();

        var every = clients.SelectMany(item => item.Machines).Select(item => item.Days).ToList();
        var counts = new RenewalCounts(
            every.Count(days => days < 0), every.Count(days => days <= 30), every.Count(days => days <= 60), every.Count(days => days <= 90));

        bool InWindow(int days) => windowDays == 0 ? days < 0 : days <= windowDays;
        var listed = clients
            .Where(item => item.Machines.Any(machine => InWindow(machine.Days)))
            .Where(item => string.IsNullOrWhiteSpace(search) || item.Row.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Machines.Min(machine => machine.Days)).ThenBy(item => item.Row.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var shown = listed.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var latest = await repository.LatestByClientAsync(managerId, shown.Select(item => item.Row.ClientId).ToList(), cancellationToken);
        var pageOfClients = shown.Select(item =>
        {
            var inWindow = item.Machines.Where(machine => InWindow(machine.Days)).ToList();
            return new RenewalClient(
                item.Row.ClientId, item.Row.Name, item.Row.Location, inWindow.Count, inWindow.Count(machine => machine.Days < 0),
                item.Machines.Select(machine => new RenewalMachine(
                    machine.Machine.EquipmentId, machine.Machine.Name, machine.Machine.SerialNumber, machine.Machine.Capacity,
                    machine.Machine.LastInspectedOn ?? (machine.Machine.LastInspectionAt is { } last ? InvoiceCalendar.Of(last) : null),
                    InvoiceCalendar.Of(machine.Machine.NextDueAt), machine.Days, InWindow(machine.Days), machine.Machine.OnOpenRequest)).ToList(),
                Recipients(item.Row), latest.TryGetValue(item.Row.ClientId, out var reminder) ? Summary(reminder) : null);
        }).ToList();

        return new RenewalsListResponse(today, counts, listed.Count, page, pageSize, pageOfClients);
    }

    private static RenewalReminderSummary Summary(RenewalReminder reminder) =>
        new(reminder.Id, reminder.State, reminder.SentAt, reminder.ToAddress, reminder.GetMachines().Count, reminder.LastReplyAt);

    /// <summary>Only addresses that exist: the certificates email first, then each contact with an email, once each.</summary>
    private static IReadOnlyList<RenewalRecipient> Recipients(RenewalClientRows client) => Recipients(client.CertificatesEmail, client.Contacts);

    private static IReadOnlyList<RenewalRecipient> Recipients(string? certificatesEmail, IReadOnlyList<RenewalContactRow> contacts)
    {
        var recipients = new List<RenewalRecipient>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(certificatesEmail) && seen.Add(certificatesEmail.Trim()))
            recipients.Add(new RenewalRecipient(certificatesEmail.Trim(), "certificates_email", null, null, null));
        foreach (var contact in contacts.Where(contact => !string.IsNullOrWhiteSpace(contact.Email)))
            if (seen.Add(contact.Email.Trim()))
                recipients.Add(new RenewalRecipient(contact.Email.Trim(), "contact", contact.Id, contact.Name, contact.Role));
        return recipients;
    }

    private static int ParseWindow(string? window) => (window ?? "30").Trim().ToLowerInvariant() switch
    {
        "overdue" => 0,
        "30" => 30,
        "60" => 60,
        "90" => 90,
        _ => throw new AuthException("invalid_window", "The window is overdue, 30, 60 or 90.", 400)
    };

    // ------------------------------------------------------------------------------------------ the reminder

    /// <summary>What a reminder is made of once the request is checked: the client, the recipient and the machines as they will be shown.</summary>
    private sealed record Checked(RenewalClientInfo Client, RenewalRecipient Recipient, IReadOnlyList<ReminderMachine> Machines, string Subject, string Message);

    private async Task<Checked> CheckAsync(Guid managerId, SendRenewalReminderRequest request, CancellationToken cancellationToken)
    {
        if (request.EquipmentIds is null || request.EquipmentIds.Count == 0)
            throw new AuthException("machines_required", "Choose at least one machine.", 400);
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new AuthException("message_required", "Write the message of the reminder.", 400);
        if (request.Message.Length > CertificateDeliveryService.MaxBodyLength)
            throw new AuthException("message_too_long", $"The message is longer than {CertificateDeliveryService.MaxBodyLength} characters.", 400);

        var client = await repository.GetClientAsync(managerId, request.ClientId, cancellationToken)
            ?? throw new KeyNotFoundException("Client was not found.");

        // The saved addresses of the client only: no typed address, no copies.
        var recipient = Recipients(client.CertificatesEmail, client.Contacts)
            .FirstOrDefault(item => string.Equals(item.Email, request.ToAddress?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new AuthException("recipient_not_of_client", "A reminder goes only to the certificates email or a contact of the client.", 400);

        var ids = request.EquipmentIds.Distinct().ToList();
        var found = (await repository.GetMachinesAsync(managerId, ids, cancellationToken)).ToDictionary(machine => machine.Id);
        var problems = new List<string>();
        var machines = new List<ReminderMachine>();
        foreach (var id in ids)
        {
            if (!found.TryGetValue(id, out var machine) || machine.ClientId != client.Id) problems.Add($"{id}: not a machine of this client");
            else if (machine.Archived) problems.Add($"{machine.Name}: archived");
            else if (machine.Retired) problems.Add($"{machine.Name}: out of service");
            else if (machine.NextDueAt is not { } due) problems.Add($"{machine.Name}: no next inspection date");
            else
                machines.Add(new ReminderMachine(
                    machine.Id, machine.Name, machine.SerialNumber,
                    machine.LastInspectedOn ?? (machine.LastInspectionAt is { } last ? InvoiceCalendar.Of(last) : null), InvoiceCalendar.Of(due)));
        }
        if (problems.Count > 0)
            throw new AuthException("machine_not_eligible", "These machines cannot go in the reminder: " + string.Join("; ", problems) + ".", 400);

        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? RenewalReminderEmail.DefaultSubject(machines.Count, string.IsNullOrWhiteSpace(client.Location) ? client.Name : client.Location!)
            : CertificateDeliveryService.CleanSubject(request.Subject);
        return new Checked(client, recipient, machines, subject, request.Message.Trim());
    }

    public async Task<RenewalReminderPreview> PreviewReminderAsync(Guid managerId, Guid actorId, SendRenewalReminderRequest request, CancellationToken cancellationToken = default)
    {
        var checkedRequest = await CheckAsync(managerId, request, cancellationToken);
        var coordinator = await userRepository.GetByIdAsync(actorId, cancellationToken);
        var config = await configRepository.GetByCoordinatorAsync(actorId, cancellationToken);
        var fromAddress = config is null || coordinator is null ? null : QuoteDeliveryService.ResolveFromAddress(config, coordinator);

        var blockers = new List<string>();
        if (options is { Enabled: false }) blockers.Add("Emailing is switched off.");
        else if (config is null) blockers.Add("The coordinator mailbox is not configured.");

        var company = companyBranding is null ? null : await companyBranding.GetAsync(managerId, cancellationToken);
        var laidOut = EmailLayout.ForCoordinator(
            checkedRequest.Message, coordinator, fromAddress, null, EmailLayout.BrandingFor(company, branding), EmailLayout.PreviewLogoFor(managerId, company),
            RenewalReminderEmail.Table(checkedRequest.Machines));
        return new RenewalReminderPreview(checkedRequest.Subject, fromAddress, coordinator?.FullName, checkedRequest.Recipient.Email, laidOut.Html, laidOut.Text, blockers);
    }

    public async Task<RenewalReminderResult> SendReminderAsync(Guid managerId, Guid actorId, SendRenewalReminderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SendKey is not { } sendKey || sendKey == Guid.Empty)
            throw new AuthException("send_key_required", "The send key is required: it makes a double click send once.", 400);
        var checkedRequest = await CheckAsync(managerId, request, cancellationToken);
        // Temporarily switched off (QuoteDelivery:Enabled=false): nothing is emailed.
        if (options is { Enabled: false })
            throw new AuthException("email_disabled", "Emailing is switched off.", 409);

        var config = await configRepository.GetByCoordinatorAsync(actorId, cancellationToken)
            ?? throw new AuthException("mailbox_not_configured", "Your mailbox is not connected, so the reminder cannot be emailed.", 409);
        var coordinator = await userRepository.GetByIdAsync(actorId, cancellationToken) ?? throw new KeyNotFoundException("The sender was not found.");
        var fromAddress = QuoteDeliveryService.ResolveFromAddress(config, coordinator);
        // Resolved before anything is recorded: a mailbox that must be connected again stops here with a clear 409 and leaves no row behind.
        var smtp = await QuoteDeliveryService.SmtpSettingsAsync(config, mailboxAuthenticator, secretProtector, cancellationToken);

        var existing = await repository.GetReminderBySendKeyAsync(managerId, sendKey, cancellationToken);
        var row = existing;
        if (row is null)
        {
            row = RenewalReminder.Create(managerId, request.ClientId, actorId, sendKey, QuoteDeliveryService.DomainOf(fromAddress));
            row.BeginAttempt(checkedRequest.Recipient.Email, checkedRequest.Recipient.Name, fromAddress, coordinator.FullName, checkedRequest.Subject, checkedRequest.Message, checkedRequest.Machines);
            if (!await repository.TryAddReminderAsync(row, cancellationToken))
            {
                // Two clicks at the same moment: the other one wrote the row first. Answer as if this one came second.
                row = await repository.GetReminderBySendKeyAsync(managerId, sendKey, cancellationToken)
                    ?? throw new AuthException("reminder_in_progress", "This reminder is being sent.", 409);
                return await AlreadyHandledAsync(row, cancellationToken) ?? throw new AuthException("reminder_in_progress", "This reminder is being sent. Wait for it to finish.", 409);
            }
        }
        else
        {
            if (await AlreadyHandledAsync(row, cancellationToken) is { } done) return done;
            if (row.ClientId != request.ClientId) throw new AuthException("send_key_used", "This send key belongs to another reminder.", 409);
            row.BeginAttempt(checkedRequest.Recipient.Email, checkedRequest.Recipient.Name, fromAddress, coordinator.FullName, checkedRequest.Subject, checkedRequest.Message, checkedRequest.Machines);
            await repository.SaveAsync(cancellationToken);
        }

        var table = RenewalReminderEmail.Table(checkedRequest.Machines);
        var company = companyBranding is null ? null : await companyBranding.GetAsync(managerId, cancellationToken);
        var laidOut = EmailLayout.ForCoordinator(row.Message, coordinator, fromAddress, null, EmailLayout.BrandingFor(company, branding), appendix: table);
        try
        {
            await emailSender.SendAsync(
                smtp,
                new OutgoingEmail(
                    row.FromAddress, row.FromName, row.ToAddress, row.ToName, row.Subject, laidOut.Text, row.MessageId,
                    null, null, null, null, BodyHtml: laidOut.Html, InlineImages: laidOut.InlineImages),
                cancellationToken);
        }
        catch (Exception exception)
        {
            row.MarkFailed(exception.Message);
            await repository.SaveAsync(CancellationToken.None);
            throw new AuthException("email_delivery_failed", "The email could not be sent: " + exception.Message, 502);
        }

        // Recorded straight away: a retry with the same key finds the reminder already sent.
        row.MarkSent();
        await repository.SaveAsync(CancellationToken.None);
        return new RenewalReminderResult(await ToResponseAsync(row, cancellationToken), true);
    }

    /// <summary>A reminder with this key that is already sent (or being sent) is answered, never sent again. <c>null</c> when it can be attempted: failed, or abandoned.</summary>
    private async Task<RenewalReminderResult?> AlreadyHandledAsync(RenewalReminder row, CancellationToken cancellationToken)
    {
        if (row.Status == OutboundEmailStatus.Sent) return new RenewalReminderResult(await ToResponseAsync(row, cancellationToken), false);
        if (row.Status == OutboundEmailStatus.Pending && !row.IsStalePending)
            throw new AuthException("reminder_in_progress", "This reminder is being sent. Wait for it to finish.", 409);
        return null;
    }

    public async Task<RequestResponse> CreateRequestAsync(Guid managerId, Guid actorId, CreateRenewalRequest request, CancellationToken cancellationToken = default)
    {
        if (inspectionRequests is null) throw new InvalidOperationException("Requests are not available.");
        if (request.EquipmentIds is null || request.EquipmentIds.Count == 0)
            throw new AuthException("machines_required", "Choose at least one machine.", 400);
        var client = await repository.GetClientAsync(managerId, request.ClientId, cancellationToken) ?? throw new KeyNotFoundException("Client was not found.");

        RenewalReminder? reminder = null;
        if (request.ReminderId is { } reminderId)
        {
            reminder = await repository.GetReminderAsync(managerId, reminderId, cancellationToken) ?? throw new KeyNotFoundException("Reminder was not found.");
            if (reminder.ClientId != client.Id) throw new AuthException("reminder_of_other_client", "This reminder belongs to another client.", 400);
            if (reminder.RequestId is not null) throw new AuthException("renewal_request_exists", "A request was already created from this reminder.", 409);
            if (reminder.ClosedAt is not null) throw new AuthException("reminder_closed", "This reminder was closed.", 409);
            if (reminder.Status != OutboundEmailStatus.Sent) throw new AuthException("reminder_not_sent", "This reminder was not sent, so it has no answer to turn into a request.", 409);
        }

        // The machines must be the client's, in service and not archived; a date is not needed for a request.
        var ids = request.EquipmentIds.Distinct().ToList();
        var found = (await repository.GetMachinesAsync(managerId, ids, cancellationToken)).ToDictionary(machine => machine.Id);
        var problems = new List<string>();
        var items = new List<CreateRequestItem>();
        foreach (var id in ids)
        {
            if (!found.TryGetValue(id, out var machine) || machine.ClientId != client.Id) problems.Add($"{id}: not a machine of this client");
            else if (machine.Archived) problems.Add($"{machine.Name}: archived");
            else if (machine.Retired) problems.Add($"{machine.Name}: out of service");
            else items.Add(new CreateRequestItem { Name = machine.Name, EquipmentId = machine.Id, SerialNumber = machine.SerialNumber, Capacity = machine.Capacity, Quantity = 1 });
        }
        if (problems.Count > 0)
            throw new AuthException("machine_not_eligible", "These machines cannot go on the request: " + string.Join("; ", problems) + ".", 400);

        var create = new CreateInspectionRequest
        {
            ClientId = client.Id,
            ClientName = client.Name,
            Location = string.IsNullOrWhiteSpace(client.Location) ? client.Name : client.Location!,
            Subject = $"Renewal: {items.Count} machine{(items.Count == 1 ? string.Empty : "s")}",
            PreferredDate = request.PreferredDate,
            Items = items
        };

        async Task<RequestResponse> Create()
        {
            var created = await inspectionRequests.CreateAsync(create, actorId, managerId, cancellationToken);
            // Claimed in the same transaction: two clicks at the same moment make one request, the other is undone.
            if (reminder is not null &&
                !await repository.ClaimReminderAsync(managerId, reminder.Id, created.Id, actorId, (clock ?? TimeProvider.System).GetUtcNow(), cancellationToken))
                throw new AuthException("renewal_request_exists", "A request was already created from this reminder.", 409);
            return created;
        }

        return transactions is null ? await Create() : await transactions.RunAsync(Create, cancellationToken);
    }

    public async Task<RenewalReminderResponse> CloseReminderAsync(Guid managerId, Guid actorId, Guid reminderId, CancellationToken cancellationToken = default)
    {
        var reminder = await repository.GetReminderAsync(managerId, reminderId, cancellationToken) ?? throw new KeyNotFoundException("Reminder was not found.");
        if (reminder.RequestId is not null) throw new AuthException("renewal_request_exists", "A request was already created from this reminder.", 409);
        reminder.Close(actorId, (clock ?? TimeProvider.System).GetUtcNow());
        await repository.SaveAsync(cancellationToken);
        return await ToResponseAsync(reminder, cancellationToken);
    }

    public async Task<RenewalReminderResponse> GetReminderAsync(Guid managerId, Guid reminderId, CancellationToken cancellationToken = default) =>
        await ToResponseAsync(await repository.GetReminderAsync(managerId, reminderId, cancellationToken) ?? throw new KeyNotFoundException("Reminder was not found."), cancellationToken);

    public async Task<RenewalReminderPage> ListRemindersAsync(Guid managerId, Guid? clientId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = pageSize < 1 ? 50 : Math.Min(pageSize, MaxPageSize);
        var (items, total) = await repository.ListRemindersAsync(managerId, clientId, page, pageSize, cancellationToken);
        var replies = (await repository.RepliesAsync(items.Select(item => item.Id).ToList(), cancellationToken)).ToLookup(reply => reply.ReminderId);
        var responses = new List<RenewalReminderResponse>();
        foreach (var item in items) responses.Add(await ToResponseAsync(item, replies[item.Id].ToList(), cancellationToken));
        return new RenewalReminderPage(total, responses);
    }

    private async Task<RenewalReminderResponse> ToResponseAsync(RenewalReminder row, CancellationToken cancellationToken) =>
        await ToResponseAsync(row, (await repository.RepliesAsync([row.Id], cancellationToken)).ToList(), cancellationToken);

    private async Task<RenewalReminderResponse> ToResponseAsync(RenewalReminder row, IReadOnlyList<RenewalReminderReply> replies, CancellationToken cancellationToken)
    {
        var sender = await userRepository.GetByIdAsync(row.SentById, cancellationToken);
        return new RenewalReminderResponse(
            row.Id, row.ClientId, row.State, new RenewalReminderUser(row.SentById, sender?.FullName ?? "Unknown"), row.CreatedAt, row.SentAt, row.Attempts, row.LastError,
            row.ToAddress, row.ToName, row.FromAddress, row.Subject, row.Message,
            row.GetMachines().Select(machine => new RenewalReminderMachine(machine.EquipmentId, machine.Name, machine.SerialNumber, machine.LastInspectedOn, machine.NextDueOn)).ToList(),
            replies.OrderBy(reply => reply.ReceivedAt)
                .Select(reply => new RenewalReminderReplyItem(reply.Id, reply.FromAddress, reply.FromName, reply.ReceivedAt, reply.Subject, TextOf(reply))).ToList(),
            row.RequestId, row.RequestCreatedAt, row.ClosedAt);
    }

    /// <summary>The text of the client's answer. When there is only HTML, the text is made from it: the client's HTML itself is never returned.</summary>
    private static string TextOf(RenewalReminderReply reply)
    {
        if (!string.IsNullOrWhiteSpace(reply.TextBody)) return reply.TextBody;
        if (string.IsNullOrWhiteSpace(reply.HtmlBody)) return string.Empty;
        var withBreaks = Regex.Replace(reply.HtmlBody, @"<\s*(br|/p|/div|/tr|/li)\s*/?>", "\n", RegexOptions.IgnoreCase);
        var withoutTags = Regex.Replace(withBreaks, @"<(script|style)[^>]*>.*?</\1>|<[^>]+>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return Regex.Replace(WebUtility.HtmlDecode(withoutTags), @"\n{3,}", "\n\n").Trim();
    }
}
