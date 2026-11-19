using Inspecta.Application.Contracts.Renewals;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;

namespace Inspecta.Application.Services;

/// <summary>The machines that are due, grouped by client, for the coordinator to remind. Days are counted in the Cairo calendar, as on the executive screens.</summary>
public sealed class RenewalsService(IRenewalsRepository repository, TimeProvider? clock = null) : IRenewalsService
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

        var pageOfClients = listed.Skip((page - 1) * pageSize).Take(pageSize).Select(item =>
        {
            var inWindow = item.Machines.Where(machine => InWindow(machine.Days)).ToList();
            return new RenewalClient(
                item.Row.ClientId, item.Row.Name, item.Row.Location, inWindow.Count, inWindow.Count(machine => machine.Days < 0),
                item.Machines.Select(machine => new RenewalMachine(
                    machine.Machine.EquipmentId, machine.Machine.Name, machine.Machine.SerialNumber, machine.Machine.Capacity,
                    machine.Machine.LastInspectedOn ?? (machine.Machine.LastInspectionAt is { } last ? InvoiceCalendar.Of(last) : null),
                    InvoiceCalendar.Of(machine.Machine.NextDueAt), machine.Days, InWindow(machine.Days), machine.Machine.OnOpenRequest)).ToList(),
                Recipients(item.Row), null);
        }).ToList();

        return new RenewalsListResponse(today, counts, listed.Count, page, pageSize, pageOfClients);
    }

    /// <summary>Only addresses that exist: the certificates email first, then each contact with an email, once each.</summary>
    private static IReadOnlyList<RenewalRecipient> Recipients(RenewalClientRows client)
    {
        var recipients = new List<RenewalRecipient>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(client.CertificatesEmail) && seen.Add(client.CertificatesEmail.Trim()))
            recipients.Add(new RenewalRecipient(client.CertificatesEmail.Trim(), "certificates_email", null, null, null));
        foreach (var contact in client.Contacts.Where(contact => !string.IsNullOrWhiteSpace(contact.Email)))
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
}
