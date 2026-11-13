using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>Revenue: the money recorded on invoices, summed by the day it was received, for the financial and general manager.</summary>
public sealed class InvoiceRevenueTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 180;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public InvoiceRevenueTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private static DateOnly Today => InvoiceCalendar.Today();
    private static DateOnly ThisMonth => new(Today.Year, Today.Month, 1);
    private static DateOnly LastMonthDay => ThisMonth.AddDays(-5);

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.88.99." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"login of {email} answered {(int)response.StatusCode}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    private sealed record Setup(Company Company, AppUser Coordinator, AppUser Finance, AppUser General, AppUser Manager);

    private Setup SeedCompany()
    {
        var number = Interlocked.Increment(ref sequence);
        var company = Company.Create($"Revenue {number}");
        api.SeedAs(db => db.Companies.Add(company));
        var manager = api.AddUser($"rev-tm-{number}@example.com", UserRole.TechnicalManager, company.Id);
        api.SeedAs(db => db.Companies.Single(item => item.Id == company.Id).SetDefaultReviewer(manager.Id));
        return new Setup(
            company,
            api.AddUser($"rev-coordinator-{number}@example.com", UserRole.Coordinator, company.Id),
            api.AddUser($"rev-fm-{number}@example.com", UserRole.FinancialManager, company.Id),
            api.AddUser($"rev-gm-{number}@example.com", UserRole.GeneralManager, company.Id),
            manager);
    }

    private Guid SeedClient(Setup setup, string name)
    {
        var client = Client.Create(name, managerId: setup.Company.Id);
        api.SeedAs(db => db.Clients.Add(client));
        return client.Id;
    }

    /// <summary>An invoice of the company for a named client, accepted long enough ago for payments in last month.</summary>
    private Invoice SeedInvoice(Setup setup, string client, Guid? clientId, long price, Action<Invoice>? pay = null)
    {
        var request = InspectionRequest.Create(client, "Cairo", clientId, [RequestItem.Create("Crane", 1)], setup.Coordinator.Id, managerId: setup.Company.Id);
        var quote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(10), setup.Coordinator.Id, clientName: client);
        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, price, "Per Visit")], null, null);
        quote.Send();
        quote.RecordClientDecision(QuoteStatus.Accepted);
        var invoice = Invoice.CreateFor(quote, request, setup.Coordinator.Id, LastMonthDay.AddDays(-10))!;
        pay?.Invoke(invoice);
        api.SeedAs(db => { db.Requests.Add(request); db.Quotes.Add(quote); db.Invoices.Add(invoice); });
        return invoice;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static string Range(DateOnly from, DateOnly to) => $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

    private static void Pay(Invoice invoice, long amount, DateOnly day) =>
        invoice.RecordPayment(amount, day, null, InvoiceTestData.Actor, Today);

    [Fact]
    public async Task Revenue_sums_the_payments_of_the_period_by_the_day_received_and_by_month()
    {
        var setup = SeedCompany();
        SeedInvoice(setup, "Client A", SeedClient(setup, "Client A"), 1_000_000, invoice =>
        {
            Pay(invoice, 200_000, LastMonthDay);
            Pay(invoice, 300_000, ThisMonth);
        });
        var general = await SignedInAs(setup.General.Email);

        var both = await Json(await general.GetAsync($"/api/v1/invoices/revenue?{Range(LastMonthDay.AddDays(-3), Today)}"));
        var lastOnly = await Json(await general.GetAsync($"/api/v1/invoices/revenue?{Range(LastMonthDay, LastMonthDay)}"));

        Assert.Equal(500_000, both.GetProperty("totalPiastres").GetInt64());
        var months = both.GetProperty("byMonth").EnumerateArray().ToArray();
        Assert.Equal([$"{LastMonthDay:yyyy-MM}", $"{ThisMonth:yyyy-MM}"], months.Select(item => item.GetProperty("month").GetString()));
        Assert.Equal([200_000L, 300_000L], months.Select(item => item.GetProperty("amountPiastres").GetInt64()));
        Assert.Equal(200_000, lastOnly.GetProperty("totalPiastres").GetInt64());
    }

    [Fact]
    public async Task A_reversal_is_dated_the_day_it_is_recorded_so_a_past_month_does_not_change()
    {
        var setup = SeedCompany();
        SeedInvoice(setup, "Client A", SeedClient(setup, "Client A"), 1_000_000, invoice =>
        {
            var wrong = invoice.RecordPayment(400_000, LastMonthDay, null, InvoiceTestData.Actor, Today);
            invoice.ReversePayment(wrong.Id, "Wrong client", InvoiceTestData.Actor, Today);
        });
        var finance = await SignedInAs(setup.Finance.Email);

        var lastMonth = await Json(await finance.GetAsync($"/api/v1/invoices/revenue?{Range(LastMonthDay, LastMonthDay)}"));
        var thisMonth = await Json(await finance.GetAsync($"/api/v1/invoices/revenue?{Range(ThisMonth, Today)}"));

        Assert.Equal(400_000, lastMonth.GetProperty("totalPiastres").GetInt64());
        Assert.Equal(-400_000, thisMonth.GetProperty("totalPiastres").GetInt64());
    }

    [Fact]
    public async Task Revenue_groups_by_client_id_else_by_the_frozen_name()
    {
        var setup = SeedCompany();
        var steel = SeedClient(setup, "Acme Steel");
        SeedInvoice(setup, "Acme Steel", steel, 1_000_000, invoice => Pay(invoice, 100_000, Today));
        SeedInvoice(setup, "Acme Steel Ltd", steel, 1_000_000, invoice => Pay(invoice, 50_000, Today));
        SeedInvoice(setup, "Walk-in", null, 1_000_000, invoice => Pay(invoice, 30_000, Today));
        SeedInvoice(setup, "walk-in ", null, 1_000_000, invoice => Pay(invoice, 20_000, Today));
        var finance = await SignedInAs(setup.Finance.Email);

        var revenue = await Json(await finance.GetAsync($"/api/v1/invoices/revenue?{Range(Today, Today)}"));

        var clients = revenue.GetProperty("byClient").EnumerateArray().ToArray();
        Assert.Equal(2, clients.Length);
        Assert.Equal(150_000, clients[0].GetProperty("amountPiastres").GetInt64());
        Assert.Equal(steel, clients[0].GetProperty("clientId").GetGuid());
        Assert.Equal(50_000, clients[1].GetProperty("amountPiastres").GetInt64());
        Assert.Equal(JsonValueKind.Null, clients[1].GetProperty("clientId").ValueKind);
    }

    [Fact]
    public async Task Revenue_is_forbidden_for_the_coordinator_and_the_technical_manager()
    {
        var setup = SeedCompany();
        foreach (var email in new[] { setup.Coordinator.Email, setup.Manager.Email })
        {
            var user = await SignedInAs(email);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/invoices/revenue")).StatusCode);
        }
    }

    [Fact]
    public async Task Revenue_defaults_to_the_current_month()
    {
        var setup = SeedCompany();
        SeedInvoice(setup, "Client A", SeedClient(setup, "Client A"), 1_000_000, invoice =>
        {
            Pay(invoice, 200_000, LastMonthDay);
            Pay(invoice, 300_000, Today);
        });
        var finance = await SignedInAs(setup.Finance.Email);

        var revenue = await Json(await finance.GetAsync("/api/v1/invoices/revenue"));

        Assert.Equal(ThisMonth.ToString("yyyy-MM-dd"), revenue.GetProperty("from").GetString());
        Assert.Equal(300_000, revenue.GetProperty("totalPiastres").GetInt64());
    }

    [Fact]
    public async Task Revenue_is_limited_to_the_callers_company_and_a_wrong_period_is_a_400()
    {
        var setup = SeedCompany();
        var stranger = SeedCompany();
        SeedInvoice(setup, "Client A", SeedClient(setup, "Client A"), 1_000_000, invoice => Pay(invoice, 700_000, Today));
        SeedInvoice(stranger, "Client B", SeedClient(stranger, "Client B"), 1_000_000, invoice => Pay(invoice, 5_000, Today));
        var finance = await SignedInAs(setup.Finance.Email);

        var revenue = await Json(await finance.GetAsync($"/api/v1/invoices/revenue?{Range(Today, Today)}"));
        var backwards = await finance.GetAsync($"/api/v1/invoices/revenue?{Range(Today, Today.AddDays(-1))}");

        Assert.Equal(700_000, revenue.GetProperty("totalPiastres").GetInt64());
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
    }
}
