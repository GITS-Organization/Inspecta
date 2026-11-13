using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The details an invoice prints that the system did not hold before: the company's tax registration number and activity code, and the client's address.</summary>
public sealed class TaxDetailsApiTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 220;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public TaxDetailsApiTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.99.11." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"login of {email} answered {(int)response.StatusCode}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    private (Company Company, AppUser CompanyAdmin, AppUser Coordinator) SeedCompany()
    {
        var number = Interlocked.Increment(ref sequence);
        var company = Company.Create($"Tax {number}");
        api.SeedAs(db => db.Companies.Add(company));
        var manager = api.AddUser($"tax-tm-{number}@example.com", UserRole.TechnicalManager, company.Id);
        api.SeedAs(db => db.Companies.Single(item => item.Id == company.Id).SetDefaultReviewer(manager.Id));
        return (company, api.AddUser($"tax-admin-{number}@example.com", UserRole.CompanyAdmin, company.Id), api.AddUser($"tax-coordinator-{number}@example.com", UserRole.Coordinator, company.Id));
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task The_company_profile_saves_and_returns_the_tax_registration_number_and_activity_code()
    {
        var setup = SeedCompany();
        var admin = await SignedInAs(setup.CompanyAdmin.Email);

        var saved = await admin.PutAsJsonAsync("/api/v1/company/profile", new
        {
            name = "Gamma Inspection", address = "5 Nile St, Cairo", taxRegistrationNumber = "  100-200-300 ", activityCode = "4610"
        });
        var read = await Json(await admin.GetAsync("/api/v1/company/profile"));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("100-200-300", (await Json(saved)).GetProperty("taxRegistrationNumber").GetString());
        Assert.Equal("100-200-300", read.GetProperty("taxRegistrationNumber").GetString());
        Assert.Equal("4610", read.GetProperty("activityCode").GetString());
    }

    [Fact]
    public async Task The_tax_details_also_save_when_the_profile_keeps_the_built_in_company_details()
    {
        var setup = SeedCompany();
        var admin = await SignedInAs(setup.CompanyAdmin.Email);

        var saved = await Json(await admin.PutAsJsonAsync("/api/v1/company/profile", new { taxRegistrationNumber = "772-901-728", activityCode = "7120" }));

        Assert.True(saved.GetProperty("usesBuiltInDetails").GetBoolean());
        Assert.Equal("772-901-728", saved.GetProperty("taxRegistrationNumber").GetString());
        Assert.Equal("7120", saved.GetProperty("activityCode").GetString());
    }

    [Fact]
    public async Task Empty_tax_details_become_null_and_a_new_save_replaces_them()
    {
        var setup = SeedCompany();
        var admin = await SignedInAs(setup.CompanyAdmin.Email);
        await admin.PutAsJsonAsync("/api/v1/company/profile", new { name = "Gamma", address = "5 Nile St", taxRegistrationNumber = "1", activityCode = "2" });

        var cleared = await Json(await admin.PutAsJsonAsync("/api/v1/company/profile", new { name = "Gamma", address = "5 Nile St", taxRegistrationNumber = "  ", activityCode = (string?)null }));

        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("taxRegistrationNumber").ValueKind);
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("activityCode").ValueKind);
    }

    [Fact]
    public async Task Too_long_tax_details_are_a_400()
    {
        var setup = SeedCompany();
        var admin = await SignedInAs(setup.CompanyAdmin.Email);

        var response = await admin.PutAsJsonAsync("/api/v1/company/profile", new { name = "Gamma", address = "5 Nile St", taxRegistrationNumber = new string('9', 65) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_client_saves_and_returns_its_address_and_empty_becomes_null()
    {
        var setup = SeedCompany();
        var coordinator = await SignedInAs(setup.Coordinator.Email);

        var created = await Json(await coordinator.PostAsJsonAsync("/api/v1/clients", new { name = "Alexandria Steel", taxNumber = "555-666", address = " 12 Port Said St, Alexandria " }));
        var id = created.GetProperty("id").GetGuid();
        var updated = await Json(await coordinator.PutAsJsonAsync($"/api/v1/clients/{id}", new { name = "Alexandria Steel", address = "14 Port Said St" }));
        var cleared = await Json(await coordinator.PutAsJsonAsync($"/api/v1/clients/{id}", new { name = "Alexandria Steel", address = "  " }));
        var read = await Json(await coordinator.GetAsync($"/api/v1/clients/{id}"));

        Assert.Equal("12 Port Said St, Alexandria", created.GetProperty("address").GetString());
        Assert.Equal("14 Port Said St", updated.GetProperty("address").GetString());
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("address").ValueKind);
        Assert.Equal(JsonValueKind.Null, read.GetProperty("address").ValueKind);
    }

    [Fact]
    public async Task A_client_address_longer_than_500_characters_is_a_400()
    {
        var setup = SeedCompany();
        var coordinator = await SignedInAs(setup.Coordinator.Email);

        var response = await coordinator.PostAsJsonAsync("/api/v1/clients", new { name = "Long Address Ltd", address = new string('a', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
