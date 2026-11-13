using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>Shared set-up of the executive HTTP tests: a company with one user per role, and helpers that seed what the screens count.</summary>
public abstract class ExecutiveApiTestBase
{
    private static int caller = 40;
    private static int sequence;
    protected readonly ApiHttpTests.Api Api;

    protected ExecutiveApiTestBase(ApiHttpTests.Api api)
    {
        Api = api;
        api.EnsureSeeded();
    }

    protected static DateOnly Today => InvoiceCalendar.Today();

    protected sealed record World(
        Company Company, AppUser General, AppUser Coordinator, AppUser Finance, AppUser CompanyAdmin, AppUser Inspector, AppUser Manager, AppUser SecondManager, AppUser Owner);

    protected World SeedCompany()
    {
        var number = Interlocked.Increment(ref sequence);
        var company = Company.Create($"Executive {number}");
        Api.SeedAs(db => db.Companies.Add(company));
        var manager = Api.AddUser($"ex-tm-{number}@example.com", UserRole.TechnicalManager, company.Id);
        var second = Api.AddUser($"ex-tm2-{number}@example.com", UserRole.TechnicalManager, company.Id);
        Api.SeedAs(db => db.Companies.Single(item => item.Id == company.Id).SetDefaultReviewer(manager.Id));
        return new World(company,
            Api.AddUser($"ex-gm-{number}@example.com", UserRole.GeneralManager, company.Id),
            Api.AddUser($"ex-coordinator-{number}@example.com", UserRole.Coordinator, company.Id),
            Api.AddUser($"ex-fm-{number}@example.com", UserRole.FinancialManager, company.Id),
            Api.AddUser($"ex-admin-{number}@example.com", UserRole.CompanyAdmin, company.Id),
            Api.AddUser($"ex-inspector-{number}@example.com", UserRole.Inspector, company.Id),
            manager, second,
            Api.AddUser($"ex-owner-{number}@example.com", UserRole.Admin));
    }

    protected async Task<HttpClient> SignedInAs(string email)
    {
        var client = Api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.66.77." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"login of {email} answered {(int)response.StatusCode}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    protected static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    protected Guid SeedClient(World world, string name, bool contract = false, string? location = "Cairo")
    {
        var client = Client.Create(name, managerId: world.Company.Id, hasContract: contract, location: location);
        Api.SeedAs(db => db.Clients.Add(client));
        return client.Id;
    }

    /// <summary>
    /// An invoice created <paramref name="ageDays"/> days ago with the given payments (days ago, amount), on a request of the company. The request
    /// can be given a reviewer and a client. Returns the invoice and its request.
    /// </summary>
    protected (Invoice Invoice, InspectionRequest Request) SeedInvoice(
        World world, long total, int ageDays, (int DaysAgo, long Amount)[]? payments = null, AppUser? reviewer = null, Guid? clientId = null,
        string clientName = "Client X", bool closeByHand = false)
    {
        var created = Today.AddDays(-ageDays);
        var request = InspectionRequest.Create(clientName, "Site", clientId, [RequestItem.Create("Crane", 1)], world.Coordinator.Id, managerId: world.Company.Id);
        if (reviewer is not null) request.UseReviewer(reviewer.Id);
        var quote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(10), world.Coordinator.Id, clientName: clientName);
        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, total, "Per Visit")], null, null);
        quote.Send();
        quote.RecordClientDecision(QuoteStatus.Accepted);
        var invoice = Invoice.CreateFor(quote, request, world.Coordinator.Id, created)!;
        foreach (var (daysAgo, amount) in payments ?? [])
            invoice.RecordPayment(amount, Today.AddDays(-daysAgo), null, world.Coordinator.Id, Today);
        if (closeByHand) invoice.CloseManually(world.Coordinator.Id, "Declined");
        Api.SeedAs(db =>
        {
            db.Requests.Add(request);
            db.Quotes.Add(quote);
            db.Invoices.Add(invoice);
            db.SaveChanges();
            db.Entry(invoice).Property(nameof(Invoice.CreatedAt)).CurrentValue = new DateTimeOffset(created.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(2));
        });
        return (invoice, request);
    }

    /// <summary>A machine of a client that is due in <paramref name="dueInDays"/> days (negative = overdue). It can be archived or have no date.</summary>
    protected Guid SeedMachine(Guid clientId, int? dueInDays, CertificateResult? lastResult = CertificateResult.SafeToOperate, bool archived = false, string name = "Mobile crane")
    {
        var machine = Item.Create(clientId, name);
        Api.SeedAs(db =>
        {
            db.Items.Add(machine);
            db.SaveChanges();
            if (dueInDays is int days)
                db.Entry(machine).Property(nameof(Item.NextInspectionDueAt)).CurrentValue = (DateTimeOffset?)new DateTimeOffset(Today.AddDays(days).ToDateTime(new TimeOnly(9, 0)), TimeSpan.FromHours(2));
            db.Entry(machine).Property(nameof(Item.LastInspectionResult)).CurrentValue = lastResult;
            if (archived) db.Entry(machine).Property(nameof(Item.IsArchived)).CurrentValue = true;
            db.SaveChanges();
        });
        return machine.Id;
    }

    protected static string Range(int fromDaysAgo, int toDaysAgo = 0) =>
        $"from={Today.AddDays(-fromDaysAgo):yyyy-MM-dd}&to={Today.AddDays(-toDaysAgo):yyyy-MM-dd}";
}
