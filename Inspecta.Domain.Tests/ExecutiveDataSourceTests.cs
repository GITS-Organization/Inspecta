using System.Security.Claims;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>The rows the executive screens are worked out from: only the company's own, and nothing a request says.</summary>
public sealed class ExecutiveDataSourceTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;

    public ExecutiveDataSourceTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        using var db = CreateContext("admin", null);
        db.Database.EnsureCreated();
    }

    private InspectaDbContext CreateContext(string role, Guid? tenantId) => Open(role, tenantId).Db;

    private (InspectaDbContext Db, EfExecutiveDataSource Source) Open(string role, Guid? tenantId)
    {
        var claims = new List<Claim> { new("role", role) };
        if (tenantId is Guid id) claims.Add(new Claim("tenant_id", id.ToString()));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
        var current = new CurrentTenant(accessor);
        var db = new InspectaDbContext(options, current);
        return (db, new EfExecutiveDataSource(db, current));
    }

    private sealed record Seeded(Guid Company, Client Client, InspectionRequest Request, Quote Quote, Invoice Invoice, Item Machine, AppUser Coordinator);

    private Seeded SeedCompany(string name, bool addMaybe = false)
    {
        var company = Company.Create(name);
        var coordinator = AppUser.Create($"{name}-c@example.com".Replace(' ', '-'), $"Coordinator {name}", UserRole.Coordinator, managerId: company.Id);
        var client = Client.Create($"{name} client", managerId: company.Id, location: "Cairo", hasContract: true);
        var request = InspectionRequest.Create(client.Name, "Secret site", client.Id, [RequestItem.Create("Crane", 2)], coordinator.Id, managerId: company.Id, subject: "Private subject", accessNotes: "gate code 1234");
        request.TransitionTo(JobStatus.QuoteDraft, coordinator.Id);
        var machine = Item.Create(client.Id, "Mobile crane");
        using var db = CreateContext("admin", null);
        db.Companies.Add(company);
        db.Users.Add(coordinator);
        db.Clients.Add(client);
        db.Requests.Add(request);
        db.Items.Add(machine);
        db.SaveChanges();
        db.Entry(machine).Property(nameof(Item.NextInspectionDueAt)).CurrentValue = (DateTimeOffset?)DateTimeOffset.UtcNow.AddDays(10);
        db.Entry(machine).Property(nameof(Item.LastInspectionResult)).CurrentValue = (CertificateResult?)CertificateResult.SafeToOperate;
        db.SaveChanges();
        var ownQuote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(10), coordinator.Id, clientName: client.Name);
        ownQuote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, 10_000, "Per Visit", "secret scope text")], "private message", null);
        ownQuote.SetVat(14m);
        ownQuote.Send();
        db.Quotes.Add(ownQuote);
        var ownInvoice = Invoice.CreateFor(AcceptedCopy(request, coordinator.Id, client.Name), request, coordinator.Id, new DateOnly(2026, 11, 1))!;
        db.Invoices.Add(ownInvoice);
        if (addMaybe)
        {
            var maybe = InspectionRequest.CreateFromEmail(company.Id, coordinator.Id, new IncomingEmail(
                "7", 1, "text", null, "x@y.com", "X", "Maybe", [], [], [], [], [], "<m@y.com>", null, null, DateTimeOffset.UtcNow), triage: RequestTriage.Maybe);
            db.Requests.Add(maybe);
        }

        db.SaveChanges();
        return new Seeded(company.Id, client, request, ownQuote, ownInvoice, machine, coordinator);
    }

    private static Quote AcceptedCopy(InspectionRequest request, Guid actor, string client)
    {
        var accepted = Quote.Create(request.Id, 2, 0, DateTimeOffset.UtcNow.AddDays(10), actor, clientName: client);
        accepted.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, 10_000, "Per Visit")], null, null);
        accepted.Send();
        accepted.RecordClientDecision(QuoteStatus.Accepted);
        return accepted;
    }

    [Fact]
    public async Task Each_company_gets_only_its_own_rows_and_the_platform_owner_gets_all_or_one()
    {
        var a = SeedCompany("Alpha");
        var b = SeedCompany("Beta");

        await using var asA = Open("general_manager", a.Company).Db;
        var rowsA = await Open("general_manager", a.Company).Source.LoadAsync(null);
        var asOtherAsking = await Open("general_manager", a.Company).Source.LoadAsync(b.Company);
        var (ownerDb, ownerSource) = Open("admin", null);
        await using var owner = ownerDb;
        var all = await ownerSource.LoadAsync(null);
        var one = await ownerSource.LoadAsync(b.Company);
        var nobody = await Open("general_manager", null).Source.LoadAsync(null);

        Assert.Equal([a.Invoice.Id], rowsA.Invoices.Select(item => item.Id));
        Assert.Equal([a.Request.Id], rowsA.Requests.Select(item => item.Id));
        Assert.Equal([a.Client.Id], rowsA.Clients.Select(item => item.Id));
        Assert.Equal([a.Machine.Id], rowsA.Machines.Select(item => item.Id));
        Assert.Equal([a.Quote.Id], rowsA.Quotes.Select(item => item.Id));
        Assert.Equal([a.Coordinator.Id], rowsA.People.Select(item => item.Id));
        Assert.Equal(2, all.Invoices.Count);
        Assert.Equal([a.Invoice.Id], asOtherAsking.Invoices.Select(item => item.Id));
        Assert.Empty(nobody.Invoices);
        Assert.Empty(nobody.People);
        Assert.Equal([b.Invoice.Id], one.Invoices.Select(item => item.Id));
        Assert.Equal([b.Request.Id], one.Requests.Select(item => item.Id));
    }

    [Fact]
    public async Task Requests_carry_their_status_changes_and_a_short_number_but_no_text()
    {
        var a = SeedCompany("Gamma", addMaybe: true);

        var (db, source) = Open("general_manager", a.Company);
        await using var _ = db;
        var data = await source.LoadAsync(null);

        var request = data.Requests.Single(item => item.Id == a.Request.Id);
        Assert.StartsWith("REQ-", request.Number);
        Assert.Equal(10, request.Number.Length);
        Assert.Contains(request.Transitions, entry => entry.To == JobStatus.QuoteDraft);
        Assert.Equal(2, request.MachineCount);
        Assert.Contains(data.Requests, item => item.Triage == RequestTriage.Maybe);
        var text = System.Text.Json.JsonSerializer.Serialize(data);
        Assert.DoesNotContain("Private subject", text);
        Assert.DoesNotContain("gate code", text);
        Assert.DoesNotContain("secret scope text", text);
        Assert.DoesNotContain("private message", text);
        Assert.DoesNotContain("Secret site", text);
    }

    [Fact]
    public async Task Quotes_carry_the_amount_with_vat_and_machines_only_when_active_and_dated()
    {
        var a = SeedCompany("Delta");
        using (var db = CreateContext("admin", null))
        {
            var undated = Item.Create(a.Client.Id, "Never inspected");
            var archived = Item.Create(a.Client.Id, "Archived crane");
            db.Items.AddRange(undated, archived);
            db.SaveChanges();
            db.Entry(archived).Property(nameof(Item.NextInspectionDueAt)).CurrentValue = (DateTimeOffset?)DateTimeOffset.UtcNow.AddDays(3);
            db.Entry(archived).Property(nameof(Item.IsArchived)).CurrentValue = true;
            db.SaveChanges();
        }

        var (read, source) = Open("general_manager", a.Company);
        await using var _ = read;
        var data = await source.LoadAsync(null);

        Assert.Equal([a.Machine.Id], data.Machines.Select(item => item.Id));
        Assert.Equal(11_400, data.Quotes.Single().TotalWithVatPiastres);
        Assert.False(data.Quotes.Single().IsPlaceholder);
    }

    public void Dispose() => connection.Dispose();
}
