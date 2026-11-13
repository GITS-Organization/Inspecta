using Inspecta.Application.Contracts.Executive;
using Inspecta.Application.Services;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Executive;

/// <summary>
/// The company's activity feed and what is coming in the next seven days, from status changes, invoices, payments and new clients. It is built from rows that
/// carry no text a request or an email says, so none can leak into it.
/// </summary>
public static class ActivityBuilder
{
    public static ActivityKind? ParseKind(string? value) => string.IsNullOrWhiteSpace(value) || value.Trim().ToLowerInvariant() == "all"
        ? null
        : value.Trim().ToLowerInvariant() switch
        {
            "quote" => ActivityKind.Quote,
            "job" => ActivityKind.Job,
            "cert" => ActivityKind.Cert,
            "inv" => ActivityKind.Inv,
            "client" => ActivityKind.Client,
            _ => throw new ArgumentException("kind must be 'all', 'quote', 'job', 'cert', 'inv' or 'client'.")
        };

    /// <summary>Every event of the company, newest first.</summary>
    public static IReadOnlyList<ActivityItem> Events(ExecutiveData data)
    {
        var names = data.People.ToDictionary(person => person.Id, person => person.Name);
        string? NameOf(Guid? id) => id is Guid value ? names.GetValueOrDefault(value) : null;
        var requests = data.Requests.Where(request => request.Triage != RequestTriage.Maybe).ToDictionary(request => request.Id);
        var items = new List<ActivityItem>();

        foreach (var request in requests.Values)
        {
            foreach (var entry in request.Transitions.Where(entry => entry.From != entry.To))
            {
                var mapped = Map(entry.To);
                if (mapped is null) continue;
                var (kind, code) = mapped.Value;
                int? count = null;
                if (code == "certificates_approved")
                    count = Math.Max(1, data.Certificates.Count(certificate => certificate.RequestId == request.Id && certificate.IsIssued
                        && InvoiceCalendar.Of(certificate.ReviewedAt.Value) == InvoiceCalendar.Of(entry.At)));
                else if (code == "certificates_delivered")
                    count = Math.Max(1, data.Certificates.Count(certificate => certificate.RequestId == request.Id && certificate.IsIssued));
                items.Add(new ActivityItem(entry.At, kind, code, request.ClientId, request.ClientName.Trim(), request.Number, NameOf(entry.ActorId), null, count));
            }
        }

        foreach (var invoice in data.Invoices)
        {
            var number = requests.TryGetValue(invoice.RequestId, out var request) ? request.Number : null;
            foreach (var entry in invoice.Events)
            {
                var code = entry.Kind switch
                {
                    InvoiceEventKind.Created => "invoice_created",
                    InvoiceEventKind.HandedOver => "invoice_handed_over",
                    InvoiceEventKind.Closed => "invoice_closed",
                    _ => null
                };
                if (code is null) continue;
                items.Add(new ActivityItem(entry.At, ActivityKind.Inv, code, invoice.ClientId, invoice.ClientName.Trim(), number, NameOf(entry.ActorId), code == "invoice_created" ? invoice.TotalPiastres : null, null));
            }

            foreach (var payment in invoice.Payments.Where(item => item.AmountPiastres > 0))
                items.Add(new ActivityItem(payment.RecordedAt, ActivityKind.Inv, "payment_recorded", invoice.ClientId, invoice.ClientName.Trim(), number, null, payment.AmountPiastres, null));
        }

        foreach (var client in data.Clients)
            items.Add(new ActivityItem(client.CreatedAt, ActivityKind.Client, "client_added", client.Id, client.Name.Trim(), null, null, null, null));

        return items.OrderByDescending(item => item.At).ThenBy(item => item.Code, StringComparer.Ordinal).ToList();
    }

    /// <summary>A page of events: of a kind, strictly before a moment, at most <paramref name="limit"/>; with the moment to ask for the next page (or <c>null</c>).</summary>
    public static (IReadOnlyList<ActivityItem> Items, DateTimeOffset? NextBefore) Page(IReadOnlyList<ActivityItem> events, ActivityKind? kind, DateTimeOffset? before, int limit)
    {
        var matching = events.Where(item => (kind is null || item.Kind == kind) && (before is null || item.At < before)).ToList();
        var page = matching.Take(limit).ToList();
        return (page, matching.Count > page.Count && page.Count > 0 ? page[^1].At : null);
    }

    /// <summary>Inspections scheduled and quotations expiring in the next seven days.</summary>
    public static IReadOnlyList<UpcomingItem> Upcoming(ExecutiveData data, DateOnly today)
    {
        var last = today.AddDays(7);
        var names = data.People.ToDictionary(person => person.Id, person => person.Name);
        var open = data.Requests.Where(request => request.Triage != RequestTriage.Maybe && WorkFigures.InFlight(request)).ToDictionary(request => request.Id);
        var items = new List<UpcomingItem>();

        foreach (var request in open.Values.Where(item => item.ScheduledAt is not null && item.Status is JobStatus.Assigned or JobStatus.TaskAccepted or JobStatus.InProgress))
        {
            var day = InvoiceCalendar.Of(request.ScheduledAt!.Value);
            if (day < today || day > last) continue;
            items.Add(new UpcomingItem(day, "inspection", request.ClientName.Trim(), request.Number, "inspection_scheduled", request.MachineCount,
                request.InspectorId is Guid inspector ? names.GetValueOrDefault(inspector) : null, null));
        }

        foreach (var quote in data.Quotes.Where(item => !item.IsPlaceholder && item.Status == QuoteStatus.Open && item.SentAt is not null && item.SupersededAt is null && open.ContainsKey(item.RequestId)))
        {
            var day = InvoiceCalendar.Of(quote.ValidUntil);
            if (day < today || day > last) continue;
            var request = open[quote.RequestId];
            items.Add(new UpcomingItem(day, "quotationExpiry", request.ClientName.Trim(), request.Number, "quotation_expires", null, null, quote.TotalWithVatPiastres));
        }

        return items.OrderBy(item => item.On).ThenBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.ClientName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static (ActivityKind Kind, string Code)? Map(JobStatus status) => status switch
    {
        JobStatus.RequestReceived => (ActivityKind.Job, "request_received"),
        JobStatus.QuoteSent or JobStatus.CounterOfferSent => (ActivityKind.Quote, "quotation_sent"),
        JobStatus.Unassigned => (ActivityKind.Quote, "quotation_accepted"),
        JobStatus.QuoteRejected or JobStatus.ClientDeclined => (ActivityKind.Quote, "quotation_rejected"),
        JobStatus.ClientCountered => (ActivityKind.Quote, "quotation_countered"),
        JobStatus.Assigned => (ActivityKind.Job, "inspector_assigned"),
        JobStatus.InProgress => (ActivityKind.Job, "inspection_started"),
        JobStatus.CertificateSubmitted => (ActivityKind.Cert, "certificates_submitted"),
        JobStatus.CertificateReturned => (ActivityKind.Cert, "certificates_returned"),
        JobStatus.CertificateApproved => (ActivityKind.Cert, "certificates_approved"),
        JobStatus.SentToClient => (ActivityKind.Cert, "certificates_delivered"),
        _ => null
    };
}
