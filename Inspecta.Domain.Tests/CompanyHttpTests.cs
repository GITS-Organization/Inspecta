using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>A company that is not its technical manager: its id is its own, and the token, the filters and the subscription follow it.</summary>
public sealed class CompanyHttpTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 40;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public CompanyHttpTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private async Task<(HttpResponseMessage Response, HttpClient Client)> SignIn(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.30.40." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        if (response.IsSuccessStatusCode)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        }

        return (response, client);
    }

    /// <summary>A company with its own id, a technical manager inside it and one client.</summary>
    private (Company Company, AppUser Manager, Client Client) SeedCompany(string name)
    {
        var number = Interlocked.Increment(ref sequence);
        var company = Company.Create($"{name} {number}");
        api.SeedAs(db => db.Companies.Add(company));
        var manager = api.AddUser($"company-manager-{number}@example.com", UserRole.TechnicalManager, company.Id);
        var client = Client.Create($"{name} client {number}", managerId: company.Id);
        api.SeedAs(db => db.Clients.Add(client));
        return (company, manager, client);
    }

    [Fact]
    public async Task ATechnicalManagerOfACompanyWithItsOwnIdWorksInsideThatCompanyOnly()
    {
        var mine = SeedCompany("Mine");
        var other = SeedCompany("Other");
        var (response, client) = await SignIn(mine.Manager.Email);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await client.GetAsync("/api/v1/clients");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var text = await list.Content.ReadAsStringAsync();
        Assert.Contains(mine.Client.Name, text);
        Assert.DoesNotContain(other.Client.Name, text);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/clients/{mine.Client.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/clients/{other.Client.Id}")).StatusCode);
    }

    [Fact]
    public async Task TheLoginAnswerShowsTheSubscriptionOfTheCompany()
    {
        var mine = SeedCompany("Shown");
        var (response, _) = await SignIn(mine.Manager.Email);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var user = json.RootElement.GetProperty("user");

        Assert.Equal(mine.Company.Id, user.GetProperty("managerId").GetGuid());
        Assert.Equal("Active", user.GetProperty("subscriptionStatus").GetString(), ignoreCase: true);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Suspended)]
    [InlineData(SubscriptionStatus.Expired)]
    public async Task ASuspendedOrExpiredCompanyCannotSignInAndItsOpenSessionsStop(SubscriptionStatus status)
    {
        var mine = SeedCompany("Lapsing");
        var (_, client) = await SignIn(mine.Manager.Email);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/clients")).StatusCode);

        api.SeedAs(db => db.Companies.Single(company => company.Id == mine.Company.Id).UpdateSubscription(status, null));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/clients")).StatusCode);
        var (again, _) = await SignIn(mine.Manager.Email);
        Assert.Equal(HttpStatusCode.Forbidden, again.StatusCode);
    }

    [Fact]
    public async Task ASwitchedOffCompanyCannotSignIn()
    {
        var mine = SeedCompany("Off");
        api.SeedAs(db => db.Companies.Single(company => company.Id == mine.Company.Id).Deactivate());

        var (response, _) = await SignIn(mine.Manager.Email);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StaffOfACompanyWithItsOwnIdWorkInsideItToo()
    {
        var mine = SeedCompany("Staffed");
        var coordinator = api.AddUser($"staff-coordinator-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Coordinator, mine.Company.Id);
        var (response, client) = await SignIn(coordinator.Email);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await client.GetAsync("/api/v1/clients");

        Assert.Contains(mine.Client.Name, await list.Content.ReadAsStringAsync());
    }
}
