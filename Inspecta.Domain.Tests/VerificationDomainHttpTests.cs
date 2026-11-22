using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>
/// A company's own verification address over HTTP: what the public page answers on a registered host and on any other, and who may
/// list and set the addresses (setting is the platform owner only).
/// </summary>
public sealed class VerificationDomainHttpTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 10;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public VerificationDomainHttpTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private sealed record Team(Company Company, AppUser Manager, AppUser CompanyAdmin, AppUser Coordinator, AppUser Inspector, string Code, string Host);

    // A company with its people and one issued certificate, and a host that is registered to it.
    private Team NewTeam(bool registerHost = true)
    {
        var number = Interlocked.Increment(ref sequence);
        var company = Company.Create($"Domain company {number}");
        api.SeedAs(db => db.Companies.Add(company));
        var manager = api.AddUser($"domain-manager-{number}@example.com", UserRole.TechnicalManager, company.Id);
        var admin = api.AddUser($"domain-admin-{number}@example.com", UserRole.CompanyAdmin, company.Id);
        var coordinator = api.AddUser($"domain-coordinator-{number}@example.com", UserRole.Coordinator, company.Id);
        var inspector = api.AddUser($"domain-inspector-{number}@example.com", UserRole.Inspector, company.Id);
        var code = Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
        var host = $"verify{number}.company{number}.example";
        api.SeedAs(db =>
        {
            db.IssuedCertificateFiles.Add(IssuedCertificateFile.Create(
                Guid.NewGuid(), Guid.NewGuid(), company.Id, null, $"Client {number}", "Crane", $"CERT-2026-9{number:000}", code,
                new DateOnly(2026, 11, 1), new DateOnly(2030, 11, 1), "cert.pdf", [37, 80, 68, 70, 45, 49, 46, 52, (byte)number]));
            if (registerHost) db.CompanyVerificationDomains.Add(CompanyVerificationDomain.Create(company.Id, host, isPrimary: true));
        });
        return new Team(company, manager, admin, coordinator, inspector, code, host);
    }

    private static string NewAddress()
    {
        var number = Interlocked.Increment(ref caller);
        return $"10.80.{number / 250}.{number % 250 + 1}";
    }

    private async Task<HttpClient> SignIn(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", NewAddress());
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    // What a phone or a browser sends to the public page: the host it was opened on, and no sign-in.
    private async Task<(HttpStatusCode Status, string Body)> Public(string path, string? host = null, string? forwardedHost = null)
    {
        using var client = api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Forwarded-For", NewAddress());
        if (host is not null) request.Headers.Host = host;
        if (forwardedHost is not null) request.Headers.Add("X-Forwarded-Host", forwardedHost);
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> Read(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    // ---- The public page ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task ARegisteredHostAnswersForItsOwnCompanysCodeOnThePageTheJsonAndTheDownload()
    {
        var team = NewTeam();

        var page = await Public($"/verify/{team.Code}", team.Host);
        var json = await Public($"/api/v1/verify/{team.Code}", team.Host);
        var pdf = await Public($"/verify/{team.Code}/pdf", team.Host);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains("Genuine certificate", page.Body);
        Assert.Equal(HttpStatusCode.OK, json.Status);
        Assert.Contains("\"valid\"", json.Body);
        Assert.Equal(HttpStatusCode.OK, pdf.Status);
    }

    [Fact]
    public async Task ACodeOfAnotherCompanyOnARegisteredHostIsExactlyAnUnknownCode()
    {
        var mine = NewTeam();
        var other = NewTeam();

        var wrongPage = await Public($"/verify/{other.Code}", mine.Host);
        var unknownPage = await Public("/verify/0000000000000000", mine.Host);
        var wrongJson = await Public($"/api/v1/verify/{other.Code}", mine.Host);
        var unknownJson = await Public("/api/v1/verify/0000000000000000", mine.Host);
        var wrongPdf = await Public($"/verify/{other.Code}/pdf", mine.Host);
        var unknownPdf = await Public("/verify/0000000000000000/pdf", mine.Host);

        Assert.Equal(HttpStatusCode.NotFound, wrongPage.Status);
        Assert.Equal(unknownPage, wrongPage);
        Assert.Contains("Certificate not found", wrongPage.Body);
        Assert.DoesNotContain(other.Company.Name, wrongPage.Body);
        Assert.Equal(HttpStatusCode.NotFound, wrongJson.Status);
        Assert.Equal(unknownJson, wrongJson);
        Assert.DoesNotContain(other.Code, wrongJson.Body);
        Assert.Equal(HttpStatusCode.NotFound, wrongPdf.Status);
        Assert.Equal(unknownPdf.Status, wrongPdf.Status);
        Assert.Equal(System.Text.RegularExpressions.Regex.Replace(unknownPdf.Body, "\"traceId\":\"[^\"]*\"", ""), System.Text.RegularExpressions.Regex.Replace(wrongPdf.Body, "\"traceId\":\"[^\"]*\"", "")); // the trace id differs per request
    }

    [Fact]
    public async Task AHostThatIsNotRegisteredAnswersForAnyCodeAsItAlwaysDid()
    {
        var one = NewTeam();
        var two = NewTeam();

        foreach (var host in new string?[] { null, "localhost", "api.gitsplatform.com", "nobody.registered.example" })
            foreach (var code in new[] { one.Code, two.Code })
            {
                Assert.Equal(HttpStatusCode.OK, (await Public($"/verify/{code}", host)).Status);
                Assert.Equal(HttpStatusCode.OK, (await Public($"/api/v1/verify/{code}", host)).Status);
                Assert.Equal(HttpStatusCode.OK, (await Public($"/verify/{code}/pdf", host)).Status);
            }
    }

    [Fact]
    public async Task TheHostIsMatchedWithoutRegardToCaseOrPort()
    {
        var mine = NewTeam();
        var other = NewTeam();

        Assert.Equal(HttpStatusCode.NotFound, (await Public($"/verify/{other.Code}", mine.Host.ToUpperInvariant())).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Public($"/verify/{other.Code}", mine.Host + ":8443")).Status);
        Assert.Equal(HttpStatusCode.OK, (await Public($"/verify/{mine.Code}", mine.Host.ToUpperInvariant() + ":8443")).Status);
    }

    [Fact]
    public async Task XForwardedHostIsNeverTrusted()
    {
        var mine = NewTeam();
        var other = NewTeam();

        // Opened on an unregistered host, a forwarded header naming a company host changes nothing: the request host decides.
        Assert.Equal(HttpStatusCode.OK, (await Public($"/verify/{other.Code}", "localhost", forwardedHost: mine.Host)).Status);
        // Opened on a company host, a forwarded header naming another host (or the owner's own) cannot widen what it answers.
        Assert.Equal(HttpStatusCode.NotFound, (await Public($"/verify/{other.Code}", mine.Host, forwardedHost: "nobody.registered.example")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Public($"/verify/{other.Code}", mine.Host, forwardedHost: other.Host)).Status);
        Assert.Equal(HttpStatusCode.OK, (await Public($"/verify/{mine.Code}", mine.Host, forwardedHost: other.Host)).Status);
    }

    [Fact]
    public async Task AnOldHostStaysRegisteredAfterANewPrimaryIsChosenSoOldQrCodesKeepWorking()
    {
        var team = NewTeam();
        var owner = await SignInOwner();

        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/v1/company/verification-domain?companyId={team.Company.Id}", new { host = "new." + team.Host })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await Public($"/verify/{team.Code}", team.Host)).Status);
        Assert.Equal(HttpStatusCode.OK, (await Public($"/verify/{team.Code}", "new." + team.Host)).Status);
    }

    // ---- The endpoints: who may use them -----------------------------------------------------------------------------

    private async Task<HttpClient> SignInOwner() =>
        await SignIn(api.AddUser($"domain-owner-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Admin).Email);

    [Fact]
    public async Task NoSignInIsRefusedAndACoordinatorOrInspectorIsForbidden()
    {
        var team = NewTeam();
        var anonymous = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/company/verification-domains")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/api/v1/company/verification-domain", new { host = "a.example.org" })).StatusCode);

        foreach (var user in new[] { team.Coordinator, team.Inspector })
        {
            var client = await SignIn(user.Email);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/company/verification-domains")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/v1/company/verification-domain", new { host = "b.example.org" })).StatusCode);
        }
    }

    [Fact]
    public async Task TheCompanyAdminAndTheTechnicalManagerListTheirHostsButCannotChooseTheQrTarget()
    {
        var team = NewTeam();

        foreach (var user in new[] { team.CompanyAdmin, team.Manager })
        {
            var client = await SignIn(user.Email);
            var list = await Read(await client.GetAsync("/api/v1/company/verification-domains"));
            Assert.Equal(team.Host, Assert.Single(list.EnumerateArray()).GetProperty("host").GetString());
            // Only the platform owner sets it, once DNS and HTTPS are verified: a host that is not live yet must not reach printed QR codes.
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/v1/company/verification-domain", new { host = "not-live-yet.example.org" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/v1/company/verification-domain?companyId={team.Company.Id}", new { host = "not-live-yet.example.org" })).StatusCode);
        }

        Assert.Single(IdsOf(team.Company.Id));
    }

    [Fact]
    public async Task ThereIsNoDeleteEndpoint()
    {
        var team = NewTeam();
        var id = IdsOf(team.Company.Id).Single();
        var owner = await SignInOwner();

        foreach (var client in new[] { owner, await SignIn(team.Manager.Email), await SignIn(team.CompanyAdmin.Email) })
        {
            var response = await client.DeleteAsync($"/api/v1/company/verification-domains/{id}?companyId={team.Company.Id}");
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, response.StatusCode.ToString());
        }

        Assert.Single(IdsOf(team.Company.Id));
    }

    [Fact]
    public async Task APersonOfOneCompanyCannotReadAnotherCompany()
    {
        var mine = NewTeam();
        var other = NewTeam();
        var client = await SignIn(mine.Manager.Email);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/company/verification-domains?companyId={other.Company.Id}")).StatusCode);
        // Naming its own company is fine.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/company/verification-domains?companyId={mine.Company.Id}")).StatusCode);
    }

    [Fact]
    public async Task ThePlatformOwnerMustNameTheCompanyAndThenActsOnIt()
    {
        var team = NewTeam(registerHost: false);
        var client = await SignInOwner();

        var missing = await client.GetAsync("/api/v1/company/verification-domains");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("company_required", (await Read(missing)).GetProperty("code").GetString());
        var missingPut = await client.PutAsJsonAsync("/api/v1/company/verification-domain", new { host = "owner-set.example.org" });
        Assert.Equal(HttpStatusCode.BadRequest, missingPut.StatusCode);
        Assert.Equal("company_required", (await Read(missingPut)).GetProperty("code").GetString());

        var put = await client.PutAsJsonAsync($"/api/v1/company/verification-domain?companyId={team.Company.Id}", new { host = "owner-set.example.org" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var saved = await Read(put);
        Assert.True(saved.GetProperty("isPrimary").GetBoolean());
        Assert.NotEqual(Guid.Empty, saved.GetProperty("id").GetGuid());
        var list = await Read(await client.GetAsync($"/api/v1/company/verification-domains?companyId={team.Company.Id}"));
        Assert.Equal("owner-set.example.org", Assert.Single(list.EnumerateArray()).GetProperty("host").GetString());
    }

    // ---- The endpoints: what they do ---------------------------------------------------------------------------------

    [Fact]
    public async Task AnInvalidReservedOrTakenHostIsRefusedWithAClearCodeAndSavesNothing()
    {
        var mine = NewTeam(registerHost: false);
        var other = NewTeam();
        var client = await SignInOwner();

        async Task<(HttpStatusCode, string?)> Put(string? host)
        {
            var response = await client.PutAsJsonAsync($"/api/v1/company/verification-domain?companyId={mine.Company.Id}", new { host });
            return (response.StatusCode, (await Read(response)).GetProperty("code").GetString());
        }

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_host"), await Put("https://certs.example.org"));
        Assert.Equal((HttpStatusCode.BadRequest, "invalid_host"), await Put("certs_1.example.org"));
        Assert.Equal((HttpStatusCode.BadRequest, "invalid_host"), await Put("10.1.2.3"));
        Assert.Equal((HttpStatusCode.BadRequest, "invalid_host"), await Put(null));
        Assert.Equal((HttpStatusCode.BadRequest, "host_reserved"), await Put("api.gitsplatform.com"));
        Assert.Equal((HttpStatusCode.BadRequest, "host_reserved"), await Put("certs.gitsplatform.com"));
        Assert.Equal((HttpStatusCode.BadRequest, "host_reserved"), await Put("inspecta.onrender.com"));
        Assert.Equal((HttpStatusCode.Conflict, "host_taken"), await Put(other.Host));
        Assert.Equal((HttpStatusCode.Conflict, "host_taken"), await Put(other.Host.ToUpperInvariant()));

        Assert.Empty((await Read(await client.GetAsync($"/api/v1/company/verification-domains?companyId={mine.Company.Id}"))).EnumerateArray());
        Assert.Single(IdsOf(other.Company.Id)); // the other company keeps its host
    }

    [Fact]
    public async Task SettingANewPrimaryKeepsTheOldHostsAndEnablingAnOldOneAgainPromotesIt()
    {
        var team = NewTeam(registerHost: false);
        var client = await SignInOwner();
        async Task<JsonElement> Put(string host) =>
            await Read(await client.PutAsJsonAsync($"/api/v1/company/verification-domain?companyId={team.Company.Id}", new { host }));
        async Task<List<(string Host, bool Primary, Guid Id)>> List() =>
            (await Read(await client.GetAsync($"/api/v1/company/verification-domains?companyId={team.Company.Id}"))).EnumerateArray()
                .Select(row => (row.GetProperty("host").GetString()!, row.GetProperty("isPrimary").GetBoolean(), row.GetProperty("id").GetGuid())).ToList();

        var first = await Put("one.example.org");
        await Put("two.example.org");
        var afterTwo = await List();

        Assert.Equal(2, afterTwo.Count);
        Assert.Equal("two.example.org", afterTwo[0].Host); // the primary comes first
        Assert.True(afterTwo[0].Primary);
        Assert.False(afterTwo[1].Primary);
        Assert.Equal(first.GetProperty("id").GetGuid(), afterTwo[1].Id);

        var again = await Put("ONE.example.org"); // promoted, not duplicated
        Assert.Equal(first.GetProperty("id").GetGuid(), again.GetProperty("id").GetGuid());
        var afterOne = await List();
        Assert.Equal(2, afterOne.Count);
        Assert.Equal(["one.example.org"], afterOne.Where(row => row.Primary).Select(row => row.Host));

        await Put("one.example.org"); // already primary: nothing changes
        Assert.Equal(afterOne, await List());
    }

    [Fact]
    public async Task OneCompanyNeverSeesAnotherCompanysHosts()
    {
        var mine = NewTeam();
        var other = NewTeam();
        var client = await SignIn(mine.Manager.Email);

        var list = await Read(await client.GetAsync("/api/v1/company/verification-domains"));
        Assert.Equal(mine.Host, Assert.Single(list.EnumerateArray()).GetProperty("host").GetString());
        Assert.DoesNotContain(other.Host, list.GetRawText());
    }

    private List<Guid> IdsOf(Guid company)
    {
        var ids = new List<Guid>();
        api.SeedAs(db => ids.AddRange(db.CompanyVerificationDomains.Where(domain => domain.ManagerId == company).Select(domain => domain.Id)));
        return ids;
    }
}
