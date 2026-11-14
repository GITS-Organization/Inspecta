using Inspecta.Application.Executive;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

/// <summary>
/// Reads the rows of the executive screens with projections, so nothing a request or quotation says (subject, notes, email, answers) is loaded.
/// The company filters of the data layer apply to every query.
/// </summary>
public sealed class EfExecutiveDataSource(InspectaDbContext dbContext, CurrentTenant tenant) : IExecutiveDataSource
{
    public async Task<ExecutiveData> LoadAsync(Guid? requestedManagerId, CancellationToken cancellationToken = default)
    {
        // Users have no company filter in the data layer, so the scope is applied here to every query: a caller that is not the platform owner
        // only ever gets their own company, whatever was asked.
        if (!tenant.IsAdmin && tenant.TenantId is null)
            return new ExecutiveData([], [], [], [], [], [], []);
        var managerId = tenant.IsAdmin ? requestedManagerId : tenant.TenantId;

        var invoices = await LoadInvoicesAsync(managerId, cancellationToken);
        var requests = await LoadRequestsAsync(managerId, cancellationToken);

        var quotes = await dbContext.Quotes.AsNoTracking()
            .Where(quote => quote.Request != null && (managerId == null || quote.Request.ManagerId == managerId))
            .Select(quote => new
            {
                quote.Id, quote.RequestId, quote.Version, quote.Status, quote.SentAt, quote.ClientRespondedAt, quote.SupersededAt, quote.ValidUntil,
                quote.ItemsJson, quote.AmountPiastres, quote.VatPercent, quote.ClientCounterPricePiastres
            }).ToListAsync(cancellationToken);
        var quoteRows = quotes.Select(quote =>
        {
            var vat = Domain.Entities.Quote.VatOf(quote.AmountPiastres, quote.VatPercent);
            // A placeholder (man day, contract, direct certificate) was sent and answered at creation and has no lines.
            var placeholder = quote.SentAt is not null && (quote.ItemsJson is "[]" or "" or null);
            return new ExecQuote(
                quote.Id, quote.RequestId, quote.Version, quote.Status, quote.SentAt, quote.ClientRespondedAt, quote.SupersededAt, quote.ValidUntil,
                placeholder, quote.AmountPiastres + vat, quote.ClientCounterPricePiastres);
        }).ToList();

        var certificates = await dbContext.Certificates.AsNoTracking()
            .Where(certificate => certificate.Request != null && (managerId == null || certificate.Request.ManagerId == managerId))
            .Select(certificate => new ExecCertificate(
                certificate.RequestId, certificate.SubmittedAt, certificate.ReviewedAt, certificate.FinalResult, certificate.ReviewerId, certificate.InspectorId,
                certificate.CertificateNumber != null))
            .ToListAsync(cancellationToken);

        var machines = await dbContext.Items.AsNoTracking()
            .Where(item => !item.IsArchived && item.Status == EquipmentStatus.Active && item.NextInspectionDueAt != null
                && item.Client != null && item.Client.ArchivedAt == null && (managerId == null || item.Client.ManagerId == managerId))
            .Select(item => new ExecMachine(item.Id, item.Name, item.SerialNumber, item.Capacity, item.ClientId, item.LastInspectionResult, item.NextInspectionDueAt))
            .ToListAsync(cancellationToken);

        var clients = await dbContext.Clients.AsNoTracking()
            .Where(client => managerId == null || client.ManagerId == managerId)
            .Select(client => new
            {
                client.Id, client.Name, client.Location, client.HasContract, client.CreatedAt, client.ArchivedAt,
                Contacts = client.Contacts.Select(contact => new ExecContact(contact.Name, contact.Role, contact.Email, contact.Phone)).ToList()
            }).ToListAsync(cancellationToken);
        var clientRows = clients.Select(client => new ExecClient(
            client.Id, client.Name, client.Location, client.HasContract, client.CreatedAt, client.ArchivedAt, client.Contacts)).ToList();

        var people = await dbContext.Users.AsNoTracking()
            .Where(user => user.Role != UserRole.Admin && (managerId == null || user.ManagerId == managerId
                || user.ManagerId == null && user.Id == managerId && user.Role == UserRole.TechnicalManager))
            .Select(user => new ExecPerson(user.Id, user.FullName, user.Role, user.IsActive, user.IsOnLeave ? user.LeaveUntil : null))
            .ToListAsync(cancellationToken);

        return new ExecutiveData(invoices, requests, quoteRows, certificates, machines, clientRows, people);
    }

    private async Task<IReadOnlyList<ExecInvoice>> LoadInvoicesAsync(Guid? managerId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Invoices.AsNoTracking()
            .Where(invoice => managerId == null || invoice.ManagerId == managerId)
            .Select(invoice => new
            {
                invoice.Id, invoice.Number, invoice.ManagerId, invoice.RequestId, invoice.ClientId, invoice.ClientName, invoice.TotalPiastres,
                invoice.CreatedAt, invoice.Stage, invoice.ClosedWithBalance, invoice.QuotationReference,
                Payments = invoice.Payments.Select(payment => new ExecPayment(payment.PaidOn, payment.AmountPiastres, payment.RecordedAt, payment.ReversesPaymentId, payment.RecordedBy, payment.Label)).ToList(),
                Events = invoice.Events.Select(entry => new ExecInvoiceEvent(entry.Kind, entry.At, entry.ActorId)).ToList()
            }).AsSplitQuery().ToListAsync(cancellationToken);
        return rows.Select(row => new ExecInvoice(
            row.Id, row.Number, row.ManagerId, row.RequestId, row.ClientId, row.ClientName, row.TotalPiastres, row.CreatedAt, row.Stage,
            row.ClosedWithBalance, row.Payments, row.Events, row.QuotationReference)).ToList();
    }

    private async Task<IReadOnlyList<ExecRequest>> LoadRequestsAsync(Guid? managerId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Requests.AsNoTracking()
            .Where(request => request.ManagerId != null && (managerId == null || request.ManagerId == managerId))
            .Select(request => new
            {
                request.Id, ManagerId = request.ManagerId!.Value, request.ClientId, request.ClientName, request.Kind, request.Triage, request.Status,
                request.ReviewerId, request.CoordinatorId, request.InspectorId, request.ReceivedAt, request.ScheduledAt,
                MachineCount = request.Items.Sum(item => item.Quantity),
                EquipmentIds = request.Items.Where(item => item.EquipmentId != null).Select(item => item.EquipmentId!.Value).ToList(),
                Transitions = request.History.Select(entry => new ExecTransition(entry.FromStatus, entry.ToStatus, entry.OccurredAt, entry.ActorId)).ToList()
            }).AsSplitQuery().ToListAsync(cancellationToken);
        return rows.Select(row => new ExecRequest(
            row.Id, row.ManagerId, row.ClientId, row.ClientName, row.Kind, row.Triage, row.Status, row.ReviewerId, row.CoordinatorId, row.InspectorId,
            row.ReceivedAt, row.ScheduledAt, row.MachineCount, row.EquipmentIds, row.Transitions.OrderBy(entry => entry.At).ToList())).ToList();
    }
}
