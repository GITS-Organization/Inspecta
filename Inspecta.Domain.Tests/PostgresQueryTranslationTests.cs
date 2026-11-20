using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>The queries the mailbox import runs must be translatable to PostgreSQL (checked without a database).</summary>
public sealed class PostgresQueryTranslationTests
{
    private static InspectaDbContext Background() => new(
        new DbContextOptionsBuilder<InspectaDbContext>()
            .UseNpgsql("Host=localhost;Database=inspecta_test;Username=test;Password=test")
            .Options,
        new CurrentTenant(new HttpContextAccessor()));

    [Fact]
    public void TheQueriesOfTheImportTranslateToPostgres()
    {
        using var db = Background();
        var manager = Guid.NewGuid();
        var client = Guid.NewGuid();
        var ids = new List<Guid> { Guid.NewGuid() };
        var messageIds = new List<string> { "a@b.com" };

        var queries = new[]
        {
            db.ClientMailRules.IgnoreQueryFilters().AsNoTracking().Where(rule => rule.ManagerId == manager).ToQueryString(),
            db.Clients.IgnoreQueryFilters().AsNoTracking()
                .Where(item => ids.Contains(item.Id) && item.ManagerId == manager && item.ArchivedAt == null).ToQueryString(),
            db.Clients.IgnoreQueryFilters().AsNoTracking().Include(item => item.Contacts)
                .Where(item => item.ManagerId == manager && item.ArchivedAt == null).ToQueryString(),
            db.Items.IgnoreQueryFilters().AsNoTracking().Where(item => item.ClientId == client && item.ArchivedAt == null).ToQueryString(),
            db.Items.Where(item => ids.Contains(item.Id)).ToQueryString(),
            db.Requests.IgnoreQueryFilters().Include(item => item.Email).Include(item => item.Items)
                .Where(item => item.ManagerId == manager && item.Triage == RequestTriage.Maybe && item.EmailFrom != null).ToQueryString(),
            db.Clients.IgnoreQueryFilters().AsNoTracking().Where(item => item.ManagerId == manager && item.ArchivedAt == null)
                .Select(item => new { item.Id, item.Name, item.Location, item.CertificatesEmail }).ToQueryString(),
            db.ClientContacts.IgnoreQueryFilters().AsNoTracking().Where(item => ids.Contains(item.ClientId) && item.Email != null)
                .Select(item => new { item.ClientId, item.Email }).ToQueryString(),
            db.Requests.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.ManagerId == manager && item.Triage == RequestTriage.Maybe && item.EmailFrom != null)
                .Select(item => new { item.Id, item.EmailFrom }).ToQueryString(),
            db.Items.AsNoTracking().Where(item => item.ArchivedAt == null && (item.Client == null || item.Client.ArchivedAt == null)).ToQueryString(),
            db.ClientMailRules.IgnoreQueryFilters().AsNoTracking().Where(rule => rule.ClientId == null || !db.Clients.IgnoreQueryFilters()
                .Where(client => client.ArchivedAt != null).Select(client => client.Id).Contains(rule.ClientId.Value)).ToQueryString(),
            db.Requests.Include(item => item.Client).Include(item => item.Items).AsSplitQuery().OrderByDescending(item => item.ReceivedAt).ToQueryString(),
            // Renewals: the list, the reminder checks and the reply lookups of the mailbox import.
            db.Items.AsNoTracking().Where(item => item.Client!.ManagerId == manager && item.Client.ArchivedAt == null && item.NextInspectionDueAt != null && item.Status == EquipmentStatus.Active && !item.IsArchived && item.ArchivedAt == null)
                .Select(item => new { item.ClientId, item.Id, item.Name, item.SerialNumber, item.Capacity, item.LastInspectionAt, item.LastInspectedOn, Due = item.NextInspectionDueAt!.Value }).ToQueryString(),
            db.RequestItems.AsNoTracking().Where(item => item.EquipmentId != null && ids.Contains(item.EquipmentId.Value) && item.Request!.ManagerId == manager && item.Request.Triage != RequestTriage.Maybe && !new[] { JobStatus.QuoteRejected }.Contains(item.Request.Status))
                .Select(item => item.EquipmentId!.Value).Distinct().ToQueryString(),
            db.Items.AsNoTracking().Where(item => ids.Contains(item.Id) && item.Client!.ManagerId == manager)
                .Select(item => new { item.Id, item.ClientId, Retired = item.Status == EquipmentStatus.Retired, Archived = item.IsArchived || item.ArchivedAt != null, item.NextInspectionDueAt }).ToQueryString(),
            db.RenewalReminders.AsNoTracking().Where(item => item.ManagerId == manager && (client == Guid.Empty || item.ClientId == client)).ToQueryString(),
            db.RenewalReminders.IgnoreQueryFilters().Where(item => item.ManagerId == manager && item.Status == OutboundEmailStatus.Sent && messageIds.Contains(item.MessageId)).ToQueryString(),
            db.RenewalReminders.IgnoreQueryFilters().Where(item => item.ManagerId == manager && item.MessageId == "x").ToQueryString(),
            db.RenewalReminderReplies.IgnoreQueryFilters().Where(reply => reply.ManagerId == manager && reply.MessageId == "x").ToQueryString(),
            db.RenewalReminderReplies.AsNoTracking().Where(reply => ids.Contains(reply.ReminderId)).ToQueryString(),
            db.ClientContacts.AsNoTracking().Where(contact => ids.Contains(contact.ClientId) && contact.Email != null && contact.Email != "")
                .OrderByDescending(contact => contact.IsPrimary).ThenBy(contact => contact.Name)
                .Select(contact => new { contact.ClientId, contact.Id, contact.Name, contact.Role, Email = contact.Email! }).ToQueryString(),
            db.RenewalReminders.AsNoTracking().Where(item => item.ManagerId == manager && ids.Contains(item.ClientId)).ToQueryString(),
            db.RenewalReminders.AsNoTracking().Where(item => item.ManagerId == manager).Select(item => new { item.Id, item.CreatedAt }).ToQueryString(),
            db.RenewalReminders.Where(item => item.ManagerId == manager && item.Id == client && item.RequestId == null && item.ClosedAt == null).ToQueryString(),
            // The dashboard: the same queries as EfDashboardRepository.
            db.Requests.AsNoTracking().Where(item => item.Triage == RequestTriage.Request && item.ManagerId == manager && item.ClientId == client && item.InspectorId == client)
                .GroupBy(item => item.Status).Select(group => new { Status = group.Key, Count = group.Count() }).ToQueryString(),
            db.Certificates.AsNoTracking()
                .Where(item => item.ReviewedAt != null && item.CertificateNumber != null && item.ReviewedAt >= DateTimeOffset.UtcNow && item.ReviewedAt <= DateTimeOffset.UtcNow)
                .Where(item => item.Request!.ManagerId == manager).Where(item => item.Request!.ClientId == client).Where(item => item.InspectorId == client)
                .Select(item => new { item.InspectorId, At = item.ReviewedAt!.Value, item.FinalResult }).ToQueryString(),
            db.Requests.AsNoTracking().Where(item => item.Triage == RequestTriage.Request && item.InspectorId != null)
                .Where(item => item.ManagerId == manager).Where(item => item.ClientId == client).Where(item => item.InspectorId == client)
                .SelectMany(
                    item => item.History.Where(entry => entry.ToStatus == JobStatus.CertificateApproved && entry.OccurredAt >= DateTimeOffset.UtcNow && entry.OccurredAt <= DateTimeOffset.UtcNow),
                    (item, entry) => new { item.Id, Inspector = item.InspectorId!.Value, entry.OccurredAt }).ToQueryString(),
            // The date filters of the request and quotation lists.
            db.Requests.AsNoTracking().Where(item => item.ReceivedAt >= DateTimeOffset.UtcNow && item.ReceivedAt < DateTimeOffset.UtcNow.AddDays(1)).ToQueryString(),
            db.Quotes.AsNoTracking().Where(quote => quote.CreatedAt >= DateTimeOffset.UtcNow && quote.CreatedAt < DateTimeOffset.UtcNow.AddDays(1)).ToQueryString(),
            // The clients of the jobs an inspector still has in hand (EfRequestRepository.ListClientIdsInHandAsync).
            db.Requests.AsNoTracking()
                .Where(item => item.InspectorId == manager && item.ClientId != null && new[] { JobStatus.Assigned, JobStatus.InProgress }.Contains(item.Status))
                .Select(item => item.ClientId!.Value).Distinct().ToQueryString(),
            // Client replies: the same lookups as EfClientReplyRepository.
            db.OutboundEmails.AsNoTracking().Where(email => email.ManagerId == manager && new[] { "a@b.c" }.Contains(email.MessageId.ToLower()))
                .Select(email => new { email.RequestId, email.QuoteId, email.CreatedAt }).ToQueryString(),
            db.RequestEmails.AsNoTracking()
                .Where(email => email.MessageId != null && new[] { "a@b.c" }.Contains(email.MessageId.ToLower()) && email.Request!.ManagerId == manager)
                .Select(email => (Guid?)email.RequestId).ToQueryString(),
            db.ClientReplies.AsNoTracking().Where(reply => reply.ManagerId == manager && reply.MessageId != null && reply.MessageId.ToLower() == "a@b.c").ToQueryString(),
            db.Items.AsNoTracking()
                .Where(item => item.NextInspectionDueAt != null && item.NextInspectionDueAt <= DateTimeOffset.UtcNow && item.Status == EquipmentStatus.Active &&
                               !item.IsArchived && item.ArchivedAt == null && item.Client!.ArchivedAt == null)
                .Where(item => item.Client!.ManagerId == manager).Where(item => item.ClientId == client)
                .Select(item => new { item.Id, item.Name, item.SerialNumber, item.ClientId, ClientName = item.Client!.Name, Due = item.NextInspectionDueAt!.Value }).ToQueryString(),
            // The quotation content of a company (EfCompanyQuotationContentRepository): read by the company id, tracked for a save and not tracked for a read.
            db.CompanyQuotationContents.AsNoTracking().Where(content => content.CompanyId == manager).ToQueryString(),
            db.CompanyQuotationContents.Where(content => content.CompanyId == manager).ToQueryString(),
            db.Quotes.AsNoTracking().Where(quote => quote.CompanyContentJson != null && quote.Id == manager).Select(quote => quote.CompanyContentJson).ToQueryString(),
            db.QuotationDrafts.IgnoreQueryFilters().Where(draft => draft.SourceUid == 1u).ToQueryString(),
            // The report files of a job (EfJobReportRepository): lists without the bytes, by id with them, the expiry stage and the count.
            db.JobReportFiles.AsNoTracking().Where(file => file.RequestId == manager).ToQueryString(),
            db.JobReportFiles.AsNoTracking().Where(file => file.RequestId == manager).Select(file => file.ExpiresAt).ToQueryString(),
            db.JobReportFiles.AsNoTracking().Include(file => file.Body).Where(file => file.RequestId == manager && file.Id == client).ToQueryString(),
            db.JobReportFiles.AsNoTracking().Include(file => file.Body).Where(file => file.RequestId == manager).ToQueryString(),
            db.JobReportFiles.Where(file => file.RequestId == manager && file.ExpiresAt == null).ToQueryString()
        };

        Assert.All(queries, sql => Assert.False(string.IsNullOrWhiteSpace(sql)));
    }

    /// <summary>The same expression as <c>EfDashboardRepository.ListNextEquipmentDueAsync</c>: sorted and cut to ten in the database, not in memory.</summary>
    [Fact]
    public void TheNearestMachinesDueQueryIsOrderedAndLimitedByPostgres()
    {
        using var db = Background();
        var sql = db.Items.AsNoTracking()
            .Where(item => item.NextInspectionDueAt != null && item.Status == EquipmentStatus.Active &&
                           !item.IsArchived && item.ArchivedAt == null && item.Client!.ArchivedAt == null)
            .Where(item => item.Client!.ManagerId == Guid.NewGuid()).Where(item => item.ClientId == Guid.NewGuid())
            .Where(item => item.NextInspectionDueAt > DateTimeOffset.UtcNow)
            .OrderBy(item => item.NextInspectionDueAt).ThenBy(item => item.Name).ThenBy(item => item.Id)
            .Take(10)
            .Select(item => new { item.Id, item.Name, item.SerialNumber, item.ClientId, ClientName = item.Client!.Name, Due = item.NextInspectionDueAt!.Value })
            .ToQueryString();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("NextInspectionDueAt", sql);
    }
}
