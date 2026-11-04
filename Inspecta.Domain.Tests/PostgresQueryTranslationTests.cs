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
            db.QuotationDrafts.IgnoreQueryFilters().Where(draft => draft.SourceUid == 1u).ToQueryString()
        };

        Assert.All(queries, sql => Assert.False(string.IsNullOrWhiteSpace(sql)));
    }
}
