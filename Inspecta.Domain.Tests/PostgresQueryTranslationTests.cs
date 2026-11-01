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
            db.QuotationDrafts.IgnoreQueryFilters().Where(draft => draft.SourceUid == 1u).ToQueryString()
        };

        Assert.All(queries, sql => Assert.False(string.IsNullOrWhiteSpace(sql)));
    }
}
