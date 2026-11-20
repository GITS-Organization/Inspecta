using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>The quotation content of a company over HTTP: who may read and replace it, how errors look, and the coordinator's own signature.</summary>
public sealed class QuotationContentHttpTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 150;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public QuotationContentHttpTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private async Task<HttpClient> SignIn(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.60." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    private static object Body(string title = "Payment") => new
    {
        terms = new object[] { new { title, points = new[] { "Within {days} days." }, showBankAccounts = false }, new { title = "Bank", points = Array.Empty<string>(), showBankAccounts = true } },
        bankAccounts = new[] { new { beneficiary = "Nour", bank = "NBE", currency = "EGP", accountNumber = "123", iban = "", swift = "" } },
        contacts = new { addresses = new[] { "12 Nile St" }, phones = new[] { "+20 100" }, emails = new[] { "info@nour.example" }, website = "nour.example" }
    };

    private (Company Company, AppUser Manager) SeedCompany()
    {
        var number = Interlocked.Increment(ref sequence);
        var company = Company.Create($"Content company {number}");
        api.SeedAs(db => db.Companies.Add(company));
        var manager = api.AddUser($"content-manager-{number}@example.com", UserRole.TechnicalManager, company.Id);
        return (company, manager);
    }

    [Fact]
    public async Task ATechnicalManagerReadsTheDefaultThenSavesAndReadsItBackWithWhoAndWhen()
    {
        var (company, manager) = SeedCompany();
        var client = await SignIn(manager.Email);

        var first = await client.GetAsync("/api/v1/company/quotation-content");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var defaults = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(defaults.GetProperty("isDefault").GetBoolean());
        // The built-in company details are drawn for a company that has no profile yet and keeps the built-in identity, which a company
        // created with Company.Create does not: its default is empty.
        Assert.Equal(JsonValueKind.Array, defaults.GetProperty("terms").ValueKind);

        var put = await client.PutAsJsonAsync("/api/v1/company/quotation-content", Body());
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var saved = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(saved.GetProperty("isDefault").GetBoolean());
        Assert.Equal("Payment", saved.GetProperty("terms")[0].GetProperty("title").GetString());
        Assert.True(saved.GetProperty("terms")[1].GetProperty("showBankAccounts").GetBoolean());
        Assert.Equal("123", saved.GetProperty("bankAccounts")[0].GetProperty("accountNumber").GetString());
        Assert.Equal("info@nour.example", saved.GetProperty("contacts").GetProperty("emails")[0].GetString());
        Assert.Equal(manager.Id, saved.GetProperty("updatedBy").GetProperty("id").GetGuid());
        Assert.Equal(manager.FullName, saved.GetProperty("updatedBy").GetProperty("fullName").GetString());
        Assert.True(saved.TryGetProperty("updatedAt", out _));

        var again = await (await client.GetAsync("/api/v1/company/quotation-content")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(again.GetProperty("isDefault").GetBoolean());
        Assert.Equal(saved.GetProperty("terms").ToString(), again.GetProperty("terms").ToString());
        Assert.Equal(company.Id, manager.ManagerId);
    }

    [Fact]
    public async Task ACompanyAdminMayEditToo()
    {
        var client = await SignIn("company-admin-a@example.com");

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/v1/company/quotation-content", Body("By the admin"))).StatusCode);
        var read = await (await client.GetAsync("/api/v1/company/quotation-content")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("By the admin", read.GetProperty("terms")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task ACoordinatorAndAnInspectorCannotReadOrEditIt()
    {
        foreach (var role in new[] { UserRole.Coordinator, UserRole.Inspector })
        {
            var user = api.AddUser($"content-{role}-{Interlocked.Increment(ref sequence)}@example.com", role, api.ManagerA);
            var client = await SignIn(user.Email);

            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/company/quotation-content")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/v1/company/quotation-content", Body())).StatusCode);
        }
    }

    [Fact]
    public async Task AnotherCompanysIdIsRefusedAndNothingIsWrittenThere()
    {
        var client = await SignIn("manager-a@example.com");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/company/quotation-content?companyId={api.ManagerB}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/v1/company/quotation-content?companyId={api.ManagerB}", Body("Intruder"))).StatusCode);

        var other = await SignIn("manager-b@example.com");
        var read = await (await other.GetAsync("/api/v1/company/quotation-content")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain("Intruder", read.ToString());
    }

    [Fact]
    public async Task TheCompaniesDoNotSeeEachOthersContent()
    {
        var a = await SignIn("manager-a@example.com");
        var b = await SignIn("manager-b@example.com");

        await a.PutAsJsonAsync("/api/v1/company/quotation-content", Body("Only A"));

        var seenByB = await (await b.GetAsync("/api/v1/company/quotation-content")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Only A", seenByB);
    }

    [Fact]
    public async Task ThePlatformOwnerMustNameTheCompanyAndAnUnknownOneIsNotFound()
    {
        var owner = api.AddUser($"content-owner-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Admin);
        var client = await SignIn(owner.Email);

        var missing = await client.GetAsync("/api/v1/company/quotation-content");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("company_required", await missing.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/v1/company/quotation-content", Body())).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/company/quotation-content?companyId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/v1/company/quotation-content?companyId={Guid.NewGuid()}", Body())).StatusCode);

        var put = await client.PutAsJsonAsync($"/api/v1/company/quotation-content?companyId={api.ManagerA}", Body("By the owner"));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var read = await (await client.GetAsync($"/api/v1/company/quotation-content?companyId={api.ManagerA}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("By the owner", read.GetProperty("terms")[0].GetProperty("title").GetString());
        Assert.Equal(owner.Id, read.GetProperty("updatedBy").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task AnAnonymousCallerIsRefused()
    {
        using var anonymous = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/company/quotation-content")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/api/v1/company/quotation-content", Body())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/api/v1/auth/me/signature", new { jobTitle = "x" })).StatusCode);
    }

    [Fact]
    public async Task ABreakenLimitIsABadRequestWithAReadableMessageAndSavesNothing()
    {
        var (_, manager) = SeedCompany();
        var client = await SignIn(manager.Email);
        await client.PutAsJsonAsync("/api/v1/company/quotation-content", Body("Kept"));

        var response = await client.PutAsJsonAsync("/api/v1/company/quotation-content", new
        {
            terms = Array.Empty<object>(),
            bankAccounts = Array.Empty<object>(),
            contacts = new { emails = new[] { "not-an-email" } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("bad_request", text);
        Assert.Contains("email", text, StringComparison.OrdinalIgnoreCase);
        var read = await (await client.GetAsync("/api/v1/company/quotation-content")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Kept", read.GetProperty("terms")[0].GetProperty("title").GetString());
    }

    // ---- My signature -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task ACoordinatorSetsTheirOwnJobTitleAndPhoneAndMeShowsThem()
    {
        var coordinator = api.AddUser($"signature-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Coordinator, api.ManagerA);
        var client = await SignIn(coordinator.Email);

        var before = await (await client.GetAsync("/api/v1/auth/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, before.GetProperty("jobTitle").ValueKind);
        Assert.Equal(JsonValueKind.Null, before.GetProperty("phone").ValueKind);

        var put = await client.PutAsJsonAsync("/api/v1/auth/me/signature", new { jobTitle = "Senior Coordinator", phone = "+20 100 111 2222" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var answer = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Senior Coordinator", answer.GetProperty("jobTitle").GetString());
        Assert.Equal(coordinator.Email, answer.GetProperty("email").GetString());

        var me = await (await client.GetAsync("/api/v1/auth/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Senior Coordinator", me.GetProperty("jobTitle").GetString());
        Assert.Equal("+20 100 111 2222", me.GetProperty("phone").GetString());

        var cleared = await client.PutAsJsonAsync("/api/v1/auth/me/signature", new { jobTitle = "", phone = " " });
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var me2 = await (await client.GetAsync("/api/v1/auth/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, me2.GetProperty("jobTitle").ValueKind);
        Assert.Equal(JsonValueKind.Null, me2.GetProperty("phone").ValueKind);
    }

    [Fact]
    public async Task ASignatureThatIsTooLongIsABadRequestAndOnlyTheCallersOwnRowChanges()
    {
        var one = api.AddUser($"signature-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Coordinator, api.ManagerA);
        var two = api.AddUser($"signature-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Coordinator, api.ManagerA);
        var client = await SignIn(one.Email);

        var tooLong = await client.PutAsJsonAsync("/api/v1/auth/me/signature", new { jobTitle = new string('t', 101) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Contains("bad_request", await tooLong.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/v1/auth/me/signature", new { phone = new string('1', 41) })).StatusCode);

        await client.PutAsJsonAsync("/api/v1/auth/me/signature", new { jobTitle = "Mine" });
        var other = await SignIn(two.Email);
        var theirs = await (await other.GetAsync("/api/v1/auth/me")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, theirs.GetProperty("jobTitle").ValueKind);
    }
}

/// <summary>The real repository and the real tables: what is saved is read back, and a sent quotation keeps its snapshot in the database.</summary>
public sealed class QuotationContentPersistenceTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;

    public QuotationContentPersistenceTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    private InspectaDbContext CreateContext() => new(
        options,
        new CurrentTenant(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "admin")], "test")) }
        }));

    [Fact]
    public async Task TheContentOfACompanyIsSavedReplacedAndReadBackFromTheDatabase()
    {
        var companyId = Guid.NewGuid();
        var editor = AppUser.Create("editor@example.com", "Editor", UserRole.TechnicalManager);
        await using (var seed = CreateContext())
        {
            seed.Users.Add(editor);
            await seed.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var service = new QuotationContentService(new EfCompanyQuotationContentRepository(db), new Branding(), new EfUserRepository(db));
            await service.SaveAsync(companyId, new Application.Contracts.Company.SaveQuotationContentRequest
            {
                Terms = [new QuotationContentTerm("One", ["a", "b"])],
                BankAccounts = [new QuotationContentBankAccount("Nour", "NBE", "EGP", "123")],
                Contacts = new QuotationContentContacts(Emails: ["a@x.example"])
            }, editor.Id);
        }

        await using (var db = CreateContext())
        {
            var service = new QuotationContentService(new EfCompanyQuotationContentRepository(db), new Branding(), new EfUserRepository(db));
            await service.SaveAsync(companyId, new Application.Contracts.Company.SaveQuotationContentRequest { Terms = [new QuotationContentTerm("Two", [])] }, editor.Id);
        }

        await using var check = CreateContext();
        var read = await new QuotationContentService(new EfCompanyQuotationContentRepository(check), new Branding(), new EfUserRepository(check)).GetAsync(companyId);
        Assert.False(read.IsDefault);
        Assert.Equal(["Two"], read.Terms.Select(term => term.Title));
        Assert.Empty(read.BankAccounts);
        Assert.Equal(editor.Id, read.UpdatedBy!.Id);
        Assert.Equal(1, check.CompanyQuotationContents.Count());
    }

    [Fact]
    public async Task ASentQuotationKeepsItsSnapshotInTheDatabaseAndADraftHasNone()
    {
        var coordinator = AppUser.Create("c@example.com", "Coordinator", UserRole.Coordinator);
        Guid sentId;
        Guid draftId;
        await using (var db = CreateContext())
        {
            db.Users.Add(coordinator);
            var request = InspectionRequest.CreateFromEmail(Guid.NewGuid(), coordinator.Id, new IncomingEmail(
                "1", 1, "Quote", null, "b@x.example", "B", "Quote", [], [], [], [], [], "<1@x>", null, null, DateTimeOffset.UtcNow));
            db.Requests.Add(request);
            var sent = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(14), coordinator.Id);
            sent.SetDraftDetails([QuoteItem.Create(null, "Crane", 1, 1_000_000, "unit")], null, null);
            sent.Send("{\"content\":{}}");
            var draft = Quote.Create(request.Id, 2, 0, DateTimeOffset.UtcNow.AddDays(14), coordinator.Id);
            db.Quotes.AddRange(sent, draft);
            await db.SaveChangesAsync();
            (sentId, draftId) = (sent.Id, draft.Id);
        }

        await using var check = CreateContext();
        Assert.Equal("{\"content\":{}}", (await check.Quotes.AsNoTracking().SingleAsync(quote => quote.Id == sentId)).CompanyContentJson);
        Assert.Null((await check.Quotes.AsNoTracking().SingleAsync(quote => quote.Id == draftId)).CompanyContentJson);
    }

    [Fact]
    public async Task AJobTitleIsSavedWithTheUser()
    {
        var user = AppUser.Create("t@example.com", "T", UserRole.Coordinator);
        await using (var db = CreateContext())
        {
            user.SetSignatureDetails("Lead", "+20 1");
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        await using var check = CreateContext();
        var saved = await check.Users.AsNoTracking().SingleAsync(item => item.Id == user.Id);
        Assert.Equal("Lead", saved.JobTitle);
        Assert.Equal("+20 1", saved.Phone);
    }

    private sealed class Branding : Application.Interfaces.ICompanyBrandingProvider
    {
        public Task<Application.Pdf.Models.CompanyBranding?> GetAsync(Guid? managerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Application.Pdf.Models.CompanyBranding?>(null);
    }

    public void Dispose() => connection.Dispose();
}
