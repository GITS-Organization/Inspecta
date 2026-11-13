using System.Security.Claims;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>Builds invoices for tests that need them in a database: an accepted quotation and its invoice, paid and handed over as asked.</summary>
public static class InvoiceTestData
{
    public static readonly Guid Actor = Guid.NewGuid();

    public static (Quote Quote, InspectionRequest Request, Invoice Invoice) Make(
        Guid companyId, long unitPrice = 1_000_000, string client = "Client X", Guid? coordinatorId = null, DateOnly? acceptedOn = null)
    {
        var request = InspectionRequest.Create(
            client, "Cairo", null, [RequestItem.Create("Crane", 1)], coordinatorId ?? Guid.NewGuid(), managerId: companyId);
        var quote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(10), Actor, clientName: client);
        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, unitPrice, "Per Visit", "Crane inspection")], null, null);
        quote.Send();
        quote.RecordClientDecision(QuoteStatus.Accepted);
        var invoice = Invoice.CreateFor(quote, request, Actor, acceptedOn ?? new DateOnly(2026, 11, 1))!;
        return (quote, request, invoice);
    }
}

/// <summary>The invoice, its payments and its history in a real (SQLite) database, and the company filter on all three.</summary>
public sealed class InvoicePersistenceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 13);
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;

    public InvoicePersistenceTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        using var db = CreateContext("admin", null);
        db.Database.EnsureCreated();
    }

    private InspectaDbContext CreateContext(string role, Guid? tenantId)
    {
        var claims = new List<Claim> { new("role", role) };
        if (tenantId is Guid id) claims.Add(new Claim("tenant_id", id.ToString()));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) }
        };
        return new InspectaDbContext(options, new CurrentTenant(accessor));
    }

    private void Save(params Invoice[] invoices)
    {
        using var db = CreateContext("admin", null);
        db.Invoices.AddRange(invoices);
        db.SaveChanges();
    }

    [Fact]
    public void An_invoice_with_payments_and_events_round_trips()
    {
        var (_, _, invoice) = InvoiceTestData.Make(Guid.NewGuid());
        var payment = invoice.RecordPayment(200_000, Today, "Advance", InvoiceTestData.Actor, Today);
        invoice.ReversePayment(payment.Id, "wrong", InvoiceTestData.Actor, Today);
        invoice.HandOver(InvoiceTestData.Actor);
        Save(invoice);

        using var db = CreateContext("admin", null);
        var loaded = new EfInvoiceRepository(db).GetByIdAsync(invoice.Id).GetAwaiter().GetResult()!;

        Assert.Equal(invoice.Number, loaded.Number);
        Assert.Equal(invoice.TotalPiastres, loaded.TotalPiastres);
        Assert.Equal(InvoiceStage.WithFinance, loaded.Stage);
        Assert.Equal(2, loaded.Payments.Count);
        Assert.Equal(0, loaded.PaidPiastres);
        Assert.Equal([InvoiceEventKind.Created, InvoiceEventKind.HandedOver], loaded.Events.Select(item => item.Kind).Order().ToArray());
        Assert.Equal("Advance", loaded.Payments.Single(item => item.AmountPiastres > 0).Label);
        Assert.Equal(Today, loaded.Payments.Single(item => item.AmountPiastres > 0).PaidOn);
        Assert.Single(loaded.GetItems());
    }

    [Fact]
    public void A_second_invoice_for_the_same_quote_violates_the_unique_index()
    {
        var (quote, request, first) = InvoiceTestData.Make(Guid.NewGuid());
        var second = Invoice.CreateFor(quote, request, InvoiceTestData.Actor, new DateOnly(2026, 11, 1))!;
        Save(first);

        Assert.Throws<DbUpdateException>(() => Save(second));
    }

    [Fact]
    public void Another_company_cannot_read_invoices_payments_or_events()
    {
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var (_, _, invoice) = InvoiceTestData.Make(companyA);
        invoice.RecordPayment(100, Today, null, InvoiceTestData.Actor, Today);
        Save(invoice);

        using var asB = CreateContext("coordinator", companyB);
        Assert.Empty(asB.Invoices.ToList());
        Assert.Empty(asB.InvoicePayments.ToList());
        Assert.Empty(asB.InvoiceEvents.ToList());

        using var asA = CreateContext("coordinator", companyA);
        Assert.Single(asA.Invoices.ToList());
        Assert.Single(asA.InvoicePayments.ToList());
        Assert.Single(asA.InvoiceEvents.ToList());
    }

    [Fact]
    public async Task List_filters_by_stage_and_payment_state_and_pages_newest_first()
    {
        var company = Guid.NewGuid();
        var open = InvoiceTestData.Make(company, 1_000).Invoice;
        var paid = InvoiceTestData.Make(company, 1_000).Invoice;
        paid.RecordPayment(1_000, Today, null, InvoiceTestData.Actor, Today);
        paid.HandOver(InvoiceTestData.Actor);
        Save(open, paid);

        await using var db = CreateContext("coordinator", company);
        var repository = new EfInvoiceRepository(db);

        Assert.Equal(2, (await repository.ListAsync(new InvoiceListFilter())).Total);
        Assert.Equal([paid.Id], (await repository.ListAsync(new InvoiceListFilter(Stage: InvoiceStage.WithFinance))).Items.Select(item => item.Id));
        Assert.Equal([open.Id], (await repository.ListAsync(new InvoiceListFilter(PaymentState: "unpaid"))).Items.Select(item => item.Id));
        var page = await repository.ListAsync(new InvoiceListFilter(Skip: 1, Take: 1));
        Assert.Equal(2, page.Total);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task List_for_a_coordinator_hides_the_email_requests_of_other_coordinators()
    {
        var company = Guid.NewGuid();
        var mine = Guid.NewGuid();
        var (_, ownRequest, ownInvoice) = InvoiceTestData.Make(company, coordinatorId: mine);
        var (_, otherRequest, manualOfOther) = InvoiceTestData.Make(company, coordinatorId: Guid.NewGuid());
        var emailOfOther = EmailRequestInvoice(company, Guid.NewGuid());
        using (var seed = CreateContext("admin", null))
        {
            seed.Requests.Add(ownRequest);
            seed.Requests.Add(otherRequest);
            seed.Requests.Add(emailOfOther.Request);
            seed.Invoices.AddRange(ownInvoice, manualOfOther, emailOfOther.Invoice);
            seed.SaveChanges();
        }

        await using var db = CreateContext("coordinator", company);
        var visible = await new EfInvoiceRepository(db).ListAsync(new InvoiceListFilter(CoordinatorId: mine));

        Assert.Equal(2, visible.Total);
        Assert.DoesNotContain(visible.Items, item => item.Id == emailOfOther.Invoice.Id);
    }

    [Fact]
    public async Task Revenue_rows_follow_paid_on_and_include_reversals()
    {
        var company = Guid.NewGuid();
        var (_, _, invoice) = InvoiceTestData.Make(company, 1_000_000, "Client X", acceptedOn: new DateOnly(2026, 10, 1));
        var october = invoice.RecordPayment(200_000, new DateOnly(2026, 10, 20), null, InvoiceTestData.Actor, Today);
        invoice.RecordPayment(300_000, new DateOnly(2026, 11, 5), null, InvoiceTestData.Actor, Today);
        invoice.ReversePayment(october.Id, "wrong", InvoiceTestData.Actor, Today);
        Save(invoice);

        await using var db = CreateContext("coordinator", company);
        var rows = await new EfInvoiceRepository(db).RevenueAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 30), null);

        Assert.Equal(300_000, rows.Sum(row => row.AmountPiastres));
        Assert.Equal(new[] { 200_000L, -200_000L, 300_000L }.Order(), rows.Select(row => row.AmountPiastres).Order());
        Assert.All(rows, row => Assert.Equal("Client X", row.ClientName));
        var october1 = await new EfInvoiceRepository(db).RevenueAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null);
        Assert.Equal(200_000, october1.Sum(row => row.AmountPiastres));
    }

    [Fact]
    public async Task A_payment_and_an_event_added_to_a_loaded_invoice_are_inserted_not_updated()
    {
        var (_, _, invoice) = InvoiceTestData.Make(Guid.NewGuid());
        Save(invoice);

        await using (var db = CreateContext("admin", null))
        {
            var repository = new EfInvoiceRepository(db);
            var loaded = (await repository.GetByIdAsync(invoice.Id, tracking: true))!;
            loaded.RecordPayment(250_000, Today, "Advance", InvoiceTestData.Actor, Today);
            loaded.HandOver(InvoiceTestData.Actor);
            await repository.SaveAsync();
        }

        await using var check = CreateContext("admin", null);
        var saved = (await new EfInvoiceRepository(check).GetByIdAsync(invoice.Id))!;
        Assert.Equal(250_000, saved.PaidPiastres);
        Assert.Equal(InvoiceStage.WithFinance, saved.Stage);
        Assert.Equal(2, saved.Events.Count);
    }

    [Fact]
    public async Task Two_changes_made_from_the_same_loaded_state_cannot_both_be_saved()
    {
        var (_, _, invoice) = InvoiceTestData.Make(Guid.NewGuid(), 1_000);
        Save(invoice);

        await using var first = CreateContext("admin", null);
        await using var second = CreateContext("admin", null);
        var one = (await new EfInvoiceRepository(first).GetByIdAsync(invoice.Id, tracking: true))!;
        var two = (await new EfInvoiceRepository(second).GetByIdAsync(invoice.Id, tracking: true))!;
        one.RecordPayment(1_000, Today, null, InvoiceTestData.Actor, Today);
        two.RecordPayment(1_000, Today, null, InvoiceTestData.Actor, Today);

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using var check = CreateContext("admin", null);
        Assert.Equal(1_000, (await new EfInvoiceRepository(check).GetByIdAsync(invoice.Id))!.PaidPiastres);
    }

    private static (InspectionRequest Request, Invoice Invoice) EmailRequestInvoice(Guid company, Guid coordinatorId)
    {
        var request = InspectionRequest.Create(
            "Mail client", "Cairo", null, [RequestItem.Create("Crane", 1)], coordinatorId, managerId: company,
            source: RequestSource.Email, emailFrom: "buyer@example.com");
        var quote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(10), InvoiceTestData.Actor, clientName: "Mail client");
        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, 5_000, "Per Visit")], null, null);
        quote.Send();
        quote.RecordClientDecision(QuoteStatus.Accepted);
        return (request, Invoice.CreateFor(quote, request, InvoiceTestData.Actor, new DateOnly(2026, 11, 1))!);
    }

    public void Dispose() => connection.Dispose();
}
