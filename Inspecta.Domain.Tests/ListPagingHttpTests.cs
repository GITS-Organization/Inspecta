using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Inspecta.Domain.Tests;

/// <summary>Long lists can be read in pages; asked without a page they answer exactly as before, with the total in a header.</summary>
public sealed class ListPagingHttpTests : IClassFixture<ApiHttpTests.Api>
{
    private static readonly object SeedLock = new();
    private readonly ApiHttpTests.Api api;

    public ListPagingHttpTests(ApiHttpTests.Api api) => this.api = api;

    private async Task<HttpClient> SignedIn()
    {
        lock (SeedLock) if (api.ManagerA == Guid.Empty) api.Seed();
        var client = api.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "manager-c@example.com", password = "Correct-Horse-Battery-1" });
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString());
        return client;
    }

    private static string[] Names(JsonElement list) => list.EnumerateArray().Select(item => item.GetProperty("name").GetString()!).ToArray();

    [Fact]
    public async Task WithoutAPageTheWholeListComesBackAsBeforeWithItsTotal()
    {
        var client = await SignedIn();

        var response = await client.GetAsync("/api/v1/clients");

        Assert.Equal(5, Names(await response.Content.ReadFromJsonAsync<JsonElement>()).Length);
        Assert.Equal("5", response.Headers.GetValues("X-Total-Count").Single());
    }

    [Fact]
    public async Task APageComesInNameOrderWithTheTotalOfAllOfThem()
    {
        var client = await SignedIn();

        var response = await client.GetAsync("/api/v1/clients?limit=2&offset=2");

        Assert.Equal(["Paged client 3", "Paged client 4"], Names(await response.Content.ReadFromJsonAsync<JsonElement>()));
        Assert.Equal("5", response.Headers.GetValues("X-Total-Count").Single());
    }

    [Fact]
    public async Task ABadPageIsRefusedWithAClearMessage()
    {
        var client = await SignedIn();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/clients?limit=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/clients?offset=-1")).StatusCode);
    }
}
