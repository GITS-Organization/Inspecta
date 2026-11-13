using Inspecta.Application.Contracts.Executive;
using Inspecta.Application.Contracts.Invoices;
using Inspecta.Application.Services;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Executive;

/// <summary>
/// The client list of the executive screens. Health: at risk = an unpaid balance on an invoice older than 60 days and at least one overdue machine;
/// watch = either of those, or an unpaid invoice 31 to 60 days old; otherwise healthy.
/// </summary>
public static class ClientFigures
{
    public static ClientRow Row(ExecClient client, ExecutiveData data, IReadOnlyList<ExecRequest> requests, IReadOnlyList<ExecInvoice> invoices, DateOnly today)
    {
        var machines = data.Machines.Where(machine => machine.ClientId == client.Id).ToList();
        var overdue = machines.Count(machine => RenewalsFigures.DaysUntilDue(machine, today) < 0);
        var owing = MoneyFigures.StillOwed(invoices.Where(invoice => invoice.ClientId == client.Id)).ToList();
        var old = owing.Where(invoice => MoneyFigures.AgeDays(invoice, today) > 60).Sum(invoice => invoice.BalancePiastres);
        var warm = owing.Any(invoice => MoneyFigures.AgeDays(invoice, today) is > 30 and <= 60);
        var health = old > 0 && overdue > 0 ? ClientHealth.AtRisk : old > 0 || overdue > 0 || warm ? ClientHealth.Watch : ClientHealth.Healthy;
        var yearStart = new DateOnly(today.Year, 1, 1);
        var theirs = requests.Where(request => request.ClientId == client.Id).ToList();
        var reviewerId = theirs.OrderByDescending(request => request.ReceivedAt).Select(request => request.ReviewerId).FirstOrDefault(id => id is not null);
        var reviewer = data.People.FirstOrDefault(person => person.Id == reviewerId)?.Name;

        return new ClientRow(
            client.Id, client.Name, client.Location, reviewer, client.HasContract, client.CreatedAt.Year, machines.Count,
            theirs.Count(RenewalsFigures.IsOpen),
            invoices.Where(invoice => invoice.ClientId == client.Id && InvoiceCalendar.Of(invoice.CreatedAt) >= yearStart).Sum(invoice => invoice.TotalPiastres),
            owing.Sum(invoice => invoice.BalancePiastres), old, overdue,
            machines.Count == 0 ? null : machines.Min(machine => RenewalsFigures.DaysUntilDue(machine, today)), health);
    }

    /// <summary>The rows of every active (not archived) client, biggest invoiced year to date first.</summary>
    public static IReadOnlyList<ClientRow> Rows(ExecutiveData data, IReadOnlyList<ExecRequest> requests, IReadOnlyList<ExecInvoice> invoices, DateOnly today) =>
        data.Clients.Where(client => client.ArchivedAt is null)
            .Select(client => Row(client, data, requests, invoices, today))
            .OrderByDescending(row => row.InvoicedYtdPiastres).ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public static ClientCounts Counts(IReadOnlyList<ClientRow> rows) => new(
        rows.Count, rows.Count(row => row.Contract),
        rows.Count(row => row.Health == ClientHealth.Healthy), rows.Count(row => row.Health == ClientHealth.Watch), rows.Count(row => row.Health == ClientHealth.AtRisk));

    public static ClientHealth? ParseHealth(string? value) => string.IsNullOrWhiteSpace(value)
        ? null
        : value.Trim().ToLowerInvariant() switch
        {
            "healthy" => ClientHealth.Healthy,
            "watch" => ClientHealth.Watch,
            "at_risk" => ClientHealth.AtRisk,
            _ => throw new ArgumentException("health must be 'healthy', 'watch' or 'at_risk'.")
        };

    public static IReadOnlyList<ClientRow> Filter(IEnumerable<ClientRow> rows, ClientHealth? health, string? search)
    {
        var text = search?.Trim();
        return rows.Where(row => (health is null || row.Health == health)
                && (string.IsNullOrEmpty(text) || row.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || (row.City?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)))
            .ToList();
    }

    public static InvoiceSummary Summary(ExecInvoice invoice)
    {
        DateTimeOffset? Last(InvoiceEventKind kind) => invoice.Events.Where(entry => entry.Kind == kind).Select(entry => (DateTimeOffset?)entry.At).OrderByDescending(at => at).FirstOrDefault();
        var paid = invoice.PaidPiastres;
        var state = paid >= invoice.TotalPiastres ? "paid" : paid > 0 ? "partial" : "unpaid";
        var stage = invoice.Stage switch { InvoiceStage.Open => "open", InvoiceStage.WithFinance => "with_finance", _ => "closed" };
        return new InvoiceSummary(
            invoice.Id, invoice.Number, invoice.ClientName, invoice.ClientId, invoice.QuotationReference, invoice.RequestId, invoice.TotalPiastres, paid,
            invoice.BalancePiastres, state, stage, invoice.CreatedAt, Last(InvoiceEventKind.Emailed), Last(InvoiceEventKind.HandedOver), Last(InvoiceEventKind.Closed));
    }
}
