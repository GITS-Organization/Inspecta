using System.Net;
using System.Text.Json;

namespace Inspecta.Domain.Tests;

/// <summary>The public check behind a certificate's QR code, called the way a phone calls it: no sign-in.</summary>
public sealed class VerificationHttpTests : IClassFixture<ApiHttpTests.Api>
{
    private const string Code = "A1B2C3D4E5F60718";
    private static readonly object SeedLock = new();
    private readonly ApiHttpTests.Api api;

    public VerificationHttpTests(ApiHttpTests.Api api) => this.api = api;

    private HttpClient Phone()
    {
        lock (SeedLock) if (api.ManagerA == Guid.Empty) api.Seed();
        return api.CreateClient();
    }

    [Fact]
    public async Task TheScannedPageShowsTheCertificateWithNoSignInAndLocksDownWhatTheBrowserMayDo()
    {
        var response = await Phone().GetAsync($"/verify/{Code}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Genuine certificate", await response.Content.ReadAsStringAsync());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task ANameWithMarkupIsShownAsTextAndNeverAsAScript()
    {
        var html = await Phone().GetStringAsync($"/verify/{Code}");

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task TheCodeIsMatchedWithoutRegardToCaseAndAWrongOneIsNotFound()
    {
        var phone = Phone();

        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync($"/verify/{Code.ToLowerInvariant()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await phone.GetAsync("/verify/A1B2C3D4E5F60719")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await phone.GetAsync("/verify/short")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await phone.GetAsync("/verify/%27%20OR%201%3D1%20--")).StatusCode);
    }

    [Fact]
    public async Task TheDownloadReturnsExactlyTheFiledPdf()
    {
        var response = await Phone().GetAsync($"/verify/{Code}/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 37, 80, 68, 70, 45, 49, 46, 52 }, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await Phone().GetAsync("/verify/A1B2C3D4E5F60719/pdf")).StatusCode);
    }

    [Fact]
    public async Task ReportsOfTheJobAreNeverReachableFromTheCheckPage()
    {
        var phone = Phone();
        const string code = "C1B2C3D4E5F60718";
        var coordinator = api.AddUser("verify-reports-coordinator@example.com", Inspecta.Domain.Enums.UserRole.Coordinator, api.ManagerA);
        var request = Inspecta.Domain.Entities.InspectionRequest.Create(
            "Verified Client", "Cairo", null, [Inspecta.Domain.Entities.RequestItem.Create("Crane", 1)], coordinator.Id, managerId: api.ManagerA);
        var certificate = new byte[] { 37, 80, 68, 70, 45, 49, 46, 52, 9 };
        api.SeedAs(db =>
        {
            db.Requests.Add(request);
            db.JobReportFiles.Add(Inspecta.Domain.Entities.JobReportFile.Create(
                api.ManagerA, request.Id, null, coordinator.Id, "Uploader", "secret-weld-report.pdf", "application/pdf", "%PDF-1.4 the-secret-report"u8.ToArray()));
            db.IssuedCertificateFiles.Add(Inspecta.Domain.Entities.IssuedCertificateFile.Create(
                Guid.NewGuid(), request.Id, api.ManagerA, null, "Verified Client", "Crane", "CERT-2026-0777", code,
                new DateOnly(2026, 11, 1), new DateOnly(2027, 11, 1), "cert.pdf", certificate));
        });

        var html = await phone.GetStringAsync($"/verify/{code}");
        var json = await phone.GetStringAsync($"/api/v1/verify/{code}");
        var pdf = await phone.GetByteArrayAsync($"/verify/{code}/pdf");

        Assert.Contains("Genuine certificate", html);
        Assert.DoesNotContain("secret-weld-report", html);
        Assert.DoesNotContain("secret-weld-report", json);
        Assert.Equal(certificate, pdf); // the stored certificate is served exactly, with no report in it
        Assert.NotEqual(HttpStatusCode.OK, (await phone.GetAsync($"/verify/{code}/reports")).StatusCode); // there is no such address
    }

    [Fact]
    public async Task TheJsonCheckSaysWhetherItIsValidWithoutLeakingTheFile()
    {
        var phone = Phone();

        var found = await phone.GetAsync($"/api/v1/verify/{Code}");
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        using var json = JsonDocument.Parse(await found.Content.ReadAsStringAsync());
        Assert.Equal("valid", json.RootElement.GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await phone.GetAsync("/api/v1/verify/A1B2C3D4E5F60719")).StatusCode);
    }
}
