using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>
/// Tenancy rule 5, seen from outside: a technical manager of company B holds a valid token, and tries every call that names a
/// record of company A (read, change, delete) and every list. Nothing of A's may come back, nothing of A's may change.
/// Each record carries a marker text so a leak shows up wherever it appears in an answer.
/// </summary>
public sealed class CrossTenantApiTests : IClassFixture<ApiHttpTests.Api>
{
    private const string Marker = "TENANT-A-ONLY";
    private static int caller = 200;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public CrossTenantApiTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private static string Next() => Interlocked.Increment(ref sequence).ToString("000");

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.50.60." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    private Task<HttpClient> ManagerA() => SignedInAs("manager-a@example.com");

    private Task<HttpClient> ManagerB() => SignedInAs("manager-b@example.com");

    /// <summary>The record is not there for this caller: 404 (or 403), never a success, and none of A's text in the answer.</summary>
    private static async Task Denied(HttpResponseMessage response, string what, params string[] secrets)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
            $"{what}: expected 404 or 403 for another tenant, got {(int)response.StatusCode}");
        // An id the caller put in the address may come back in a "not found" answer: that is the caller's own text, not a leak.
        var address = response.RequestMessage?.RequestUri?.ToString() ?? string.Empty;
        foreach (var secret in secrets.Append(Marker).Where(secret => !address.Contains(secret, StringComparison.OrdinalIgnoreCase)))
            Assert.False(body.Contains(secret, StringComparison.OrdinalIgnoreCase), $"{what}: the answer contains '{secret}': {body}");
    }

    /// <summary>A list: whatever the status, none of A's text is in it.</summary>
    private static async Task NoLeak(HttpResponseMessage response, string what, params string[] secrets)
    {
        var body = await response.Content.ReadAsStringAsync();
        foreach (var secret in secrets.Append(Marker))
            Assert.False(body.Contains(secret, StringComparison.OrdinalIgnoreCase), $"{what} shows another tenant's data: {secret}");
    }

    [Fact]
    public async Task TheCheckFailsWhenAnotherTenantGetsASuccess()
    {
        var success = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };

        await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => Denied(success, "probe"));
    }

    [Fact]
    public async Task TheCheckFailsWhenAnotherTenantsTextIsInAnAnswer()
    {
        var leaking = new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent($"{{\"name\":\"{Marker} client\"}}") };
        var list = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"[{{\"name\":\"{Marker} client\"}}]") };

        await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => Denied(leaking, "probe"));
        await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => NoLeak(list, "probe"));
    }

    [Fact]
    public async Task ClientsOfAnotherTenantAreNotReachable()
    {
        var client = Client.Create($"{Marker} client {Next()}", managerId: api.ManagerA);
        api.SeedAs(db => db.Clients.Add(client));
        var b = await ManagerB();

        await Denied(await b.GetAsync($"/api/v1/clients/{client.Id}"), "GET client", client.Id.ToString());
        await Denied(await b.GetAsync($"/api/v1/clients/{client.Id}/history"), "GET client history");
        await Denied(await b.PutAsJsonAsync($"/api/v1/clients/{client.Id}", new { name = "Hijacked" }), "PUT client");
        await Denied(await b.DeleteAsync($"/api/v1/clients/{client.Id}"), "DELETE client");
        await Denied(await b.GetAsync($"/api/v1/clients/{client.Id}/emails"), "GET client emails");
        await NoLeak(await b.GetAsync("/api/v1/clients"), "client list", client.Id.ToString());

        var a = await ManagerA();
        var still = await a.GetAsync($"/api/v1/clients/{client.Id}");
        Assert.Equal(HttpStatusCode.OK, still.StatusCode);
        Assert.Contains(Marker, await still.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Hijacked", await still.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ClientMailRulesOfAnotherTenantAreNotReachable()
    {
        var coordinator = api.AddUser($"rules-coordinator-{Next()}@example.com", UserRole.Coordinator, api.ManagerA);
        var pattern = $"{Marker.ToLowerInvariant()}-{Next()}@a-only.example";
        var rule = ClientMailRule.Create(api.ManagerA, pattern, null, coordinator.Id);
        api.SeedAs(db => db.ClientMailRules.Add(rule));
        var b = await ManagerB();

        await Denied(await b.DeleteAsync($"/api/v1/client-mail-rules/{rule.Id}"), "DELETE rule", pattern);
        await NoLeak(await b.GetAsync("/api/v1/client-mail-rules"), "rule list", pattern);

        var a = await ManagerA();
        Assert.Contains(pattern, await (await a.GetAsync("/api/v1/client-mail-rules")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RequestsOfAnotherTenantAreNotReachable()
    {
        var coordinator = api.AddUser($"requests-coordinator-{Next()}@example.com", UserRole.Coordinator, api.ManagerA);
        var request = InspectionRequest.Create($"{Marker} client {Next()}", "Cairo", null, [RequestItem.Create("Mobile crane", 1)], coordinator.Id, managerId: api.ManagerA);
        api.SeedAs(db => db.Requests.Add(request));
        var b = await ManagerB();
        var id = request.Id;

        await Denied(await b.GetAsync($"/api/v1/requests/{id}"), "GET request", id.ToString());
        await Denied(await b.GetAsync($"/api/v1/requests/{id}/email"), "GET request email");
        await Denied(await b.GetAsync($"/api/v1/requests/{id}/suggestions"), "GET request suggestions");
        await Denied(await b.DeleteAsync($"/api/v1/requests/{id}"), "DELETE request");
        await Denied(await b.PostAsJsonAsync($"/api/v1/requests/{id}/reject", new { reason = "Not needed" }), "reject request");
        await NoLeak(await b.GetAsync("/api/v1/requests"), "request list", id.ToString());
        await NoLeak(await b.GetAsync("/api/v1/requests?triage=maybe"), "maybe list", id.ToString());
        await NoLeak(await b.GetAsync($"/api/v1/requests?managerId={api.ManagerA}"), "request list naming another manager", id.ToString());
        await NoLeak(await b.GetAsync("/api/v1/quotations"), "quotation list", id.ToString());

        var a = await ManagerA();
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/api/v1/requests/{id}")).StatusCode);
    }

    [Fact]
    public async Task QuotesOfAnotherTenantAreNotReachable()
    {
        var coordinator = api.AddUser($"quotes-coordinator-{Next()}@example.com", UserRole.Coordinator, api.ManagerA);
        var request = InspectionRequest.Create($"{Marker} client {Next()}", "Cairo", null, [RequestItem.Create("Mobile crane", 1)], coordinator.Id, managerId: api.ManagerA);
        var quote = Quote.Create(request.Id, 1, 150_000, DateTimeOffset.UtcNow.AddDays(10), coordinator.Id);
        api.SeedAs(db =>
        {
            db.Requests.Add(request);
            db.Quotes.Add(quote);
        });
        var b = await ManagerB();
        var path = $"/api/v1/requests/{request.Id}/quotes";

        await Denied(await b.GetAsync($"{path}/{quote.Id}"), "GET quote", quote.Id.ToString());
        await Denied(await b.GetAsync($"{path}/{quote.Id}/pdf"), "GET quote pdf");
        await Denied(await b.GetAsync($"{path}/{quote.Id}/email"), "GET quote email");
        await Denied(await b.PostAsync($"{path}/{quote.Id}/accept", null), "accept quote");
        await Denied(await b.PostAsync($"{path}/{quote.Id}/send", null), "send quote");
        await NoLeak(await b.GetAsync(path), "quote list of another tenant's request", quote.Id.ToString());
        await NoLeak(await b.GetAsync("/api/v1/quotations"), "quotation list", quote.Id.ToString());
    }

    [Fact]
    public async Task CertificateFilesOfAnotherTenantAreNotReachable()
    {
        var client = Client.Create($"{Marker} client {Next()}", managerId: api.ManagerA);
        var number = $"CERT-2026-9{Next()}";
        var file = IssuedCertificateFile.Create(
            Guid.NewGuid(), Guid.NewGuid(), api.ManagerA, client.Id, client.Name, $"{Marker} crane", number, "A1A1A1A1A1A1" + Next() + "AA",
            new DateOnly(2026, 11, 1), new DateOnly(2027, 11, 1), "a-only.pdf", [37, 80, 68, 70, 45, 49, 46, 52]);
        api.SeedAs(db =>
        {
            db.Clients.Add(client);
            db.IssuedCertificateFiles.Add(file);
        });
        var b = await ManagerB();
        var path = $"/api/v1/clients/{client.Id}/certificates";

        await Denied(await b.GetAsync($"{path}/{file.Id}/pdf"), "GET certificate pdf", number);
        await Denied(await b.GetAsync($"{path}/{file.Id}/qr.png"), "GET certificate qr");
        await NoLeak(await b.GetAsync(path), "certificate list of another tenant's client", number);

        var a = await ManagerA();
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"{path}/{file.Id}/pdf")).StatusCode);
    }

    [Fact]
    public async Task TeamMembersOfAnotherTenantAreNotReachable()
    {
        var email = $"{Marker.ToLowerInvariant()}-member-{Next()}@example.com";
        var member = api.AddUser(email, UserRole.Coordinator, api.ManagerA);
        var b = await ManagerB();
        var path = $"/api/v1/manager/team/{member.Id}";

        await Denied(await b.GetAsync(path), "GET team member", email);
        await Denied(await b.PutAsJsonAsync($"{path}/status", new { isActive = false }), "PUT member status");
        await Denied(await b.PutAsJsonAsync($"{path}/password", new { newPassword = "Brand-New-Passw0rd!" }), "PUT member password");
        await Denied(await b.PutAsJsonAsync($"{path}/email", new { email = "stolen@example.com" }), "PUT member email");
        await Denied(await b.DeleteAsync(path), "DELETE team member");
        await Denied(await b.GetAsync($"{path}/email-config"), "GET member mailbox settings");
        await NoLeak(await b.GetAsync("/api/v1/manager/team"), "team list", email);

        // Nothing of the member changed: the same email and password still sign in, and the account is still active.
        var login = await api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}
