using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The four report endpoints over HTTP: what a caller gets back, the status codes, the headers of a download. Files are read by id.</summary>
public sealed class JobReportsApiTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 100;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public JobReportsApiTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.70.80." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    /// <summary>A job of team A, assigned to a new inspector and already started, with the team's coordinator that owns it.</summary>
    private (Guid RequestId, AppUser Inspector, AppUser Coordinator) StartedJob()
    {
        var number = Interlocked.Increment(ref sequence);
        var coordinator = api.AddUser($"report-coordinator-{number}@example.com", UserRole.Coordinator, api.ManagerA);
        var inspector = api.AddUser($"report-inspector-{number}@example.com", UserRole.Inspector, api.ManagerA);
        var request = InspectionRequest.Create($"Report client {number}", "Cairo", null, [RequestItem.Create("Mobile crane", 1)], coordinator.Id, managerId: api.ManagerA);
        request.UseReviewer(api.ManagerA);
        request.TransitionTo(JobStatus.QuoteDraft, coordinator.Id);
        request.TransitionTo(JobStatus.QuoteSent, coordinator.Id);
        request.TransitionTo(JobStatus.Unassigned, coordinator.Id);
        request.AssignInspector(inspector.Id, DateTimeOffset.UtcNow.AddDays(1), coordinator.Id);
        request.TransitionTo(JobStatus.TaskAccepted, inspector.Id);
        request.TransitionTo(JobStatus.InProgress, inspector.Id);
        api.SeedAs(db => db.Requests.Add(request));
        return (request.Id, inspector, coordinator);
    }

    private static object Body(string name, byte[] content) => new { fileName = name, contentBase64 = Convert.ToBase64String(content) };

    [Fact]
    public async Task AnInspectorAddsListsDownloadsAndRemovesAReportOverHttp()
    {
        var (requestId, inspector, _) = StartedJob();
        var client = await SignedInAs(inspector.Email);
        var path = $"/api/v1/quotation-queue/{requestId}/reports";
        var pdf = "%PDF-1.4 over http"u8.ToArray();

        var created = await client.PostAsJsonAsync(path, Body("Weld report.pdf", pdf));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var file = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = file.GetProperty("id").GetGuid();
        Assert.Equal("Weld report.pdf", file.GetProperty("fileName").GetString());
        Assert.Equal("application/pdf", file.GetProperty("contentType").GetString());
        Assert.Equal(pdf.Length, file.GetProperty("sizeBytes").GetInt32());
        Assert.True(file.GetProperty("canDelete").GetBoolean());
        Assert.Equal($"{path}/{id}", file.GetProperty("downloadUrl").GetString());

        var list = await client.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal(1, list.GetProperty("files").GetArrayLength());
        Assert.True(list.GetProperty("canUpload").GetBoolean());
        Assert.Equal(20, list.GetProperty("maxFiles").GetInt32());
        Assert.Equal(JsonValueKind.Null, list.GetProperty("expiresAt").ValueKind);

        var machines = await client.GetFromJsonAsync<JsonElement>($"/api/v1/quotation-queue/{requestId}/machines");
        Assert.Equal(1, machines.GetProperty("reportCount").GetInt32());
        Assert.True(machines.GetProperty("canUploadReports").GetBoolean());

        var download = await client.GetAsync($"{path}/{id}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(pdf, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("Weld report.pdf", download.Content.Headers.ContentDisposition.FileNameStar ?? download.Content.Headers.ContentDisposition.FileName!.Trim('"'));
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());

        var removed = await client.DeleteAsync($"{path}/{id}");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(0, (await removed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("files").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{path}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{path}/{id}")).StatusCode);
    }

    [Fact]
    public async Task BadFilesAndTheWrongRoleGetTheDocumentedCodes()
    {
        var (requestId, inspector, coordinator) = StartedJob();
        var client = await SignedInAs(inspector.Email);
        var path = $"/api/v1/quotation-queue/{requestId}/reports";

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, Body("run.exe", "MZ program"u8.ToArray()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, Body("a.zip", [0x50, 0x4B, 3, 4]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, Body("a.pdf", []))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new { fileName = "a.pdf", contentBase64 = "!!!" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new { fileName = "a.pdf", contentBase64 = Convert.ToBase64String("%PDF-1"u8.ToArray()), requestItemId = Guid.NewGuid() })).StatusCode);

        var coordinatorClient = await SignedInAs(coordinator.Email);
        var upload = await coordinatorClient.PostAsJsonAsync(path, Body("a.pdf", "%PDF-1"u8.ToArray()));
        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
        Assert.Contains("forbidden", await upload.Content.ReadAsStringAsync());
        var early = await coordinatorClient.GetAsync(path);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Contains("not_approved", await early.Content.ReadAsStringAsync());

        var other = await SignedInAs(StartedJob().Inspector.Email);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(path, Body("a.pdf", "%PDF-1"u8.ToArray()))).StatusCode);

        using var anonymous = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task AllTheReportsComeInOneZipOverHttpWithTheSameAccessRulesAsTheList()
    {
        var (requestId, inspector, coordinator) = StartedJob();
        var client = await SignedInAs(inspector.Email);
        var path = $"/api/v1/quotation-queue/{requestId}/reports";
        var zipPath = $"{path}.zip";

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(zipPath)).StatusCode); // no report yet
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(path, Body("Weld report.pdf", "%PDF-1.4 one"u8.ToArray()))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(path, Body("Weld report.pdf", "%PDF-1.4 two"u8.ToArray()))).StatusCode);

        var zip = await client.GetAsync(zipPath);

        Assert.Equal(HttpStatusCode.OK, zip.StatusCode);
        Assert.Equal("application/zip", zip.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", zip.Content.Headers.ContentDisposition!.DispositionType);
        Assert.StartsWith("reports-", zip.Content.Headers.ContentDisposition.FileNameStar ?? zip.Content.Headers.ContentDisposition.FileName!.Trim('"'));
        Assert.Equal("nosniff", zip.Headers.GetValues("X-Content-Type-Options").Single());
        using (var archive = new System.IO.Compression.ZipArchive(new MemoryStream(await zip.Content.ReadAsByteArrayAsync())))
            Assert.Equal(["reports/Weld report (2).pdf", "reports/Weld report.pdf"], archive.Entries.Select(entry => entry.FullName).Order());

        var coordinatorClient = await SignedInAs(coordinator.Email);
        var early = await coordinatorClient.GetAsync(zipPath);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Contains("not_approved", await early.Content.ReadAsStringAsync());

        var other = await SignedInAs(StartedJob().Inspector.Email);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(zipPath)).StatusCode);
        using var anonymous = api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(zipPath)).StatusCode);
    }

    [Fact]
    public async Task AReportAddedAfterTheSubmitIsLocked()
    {
        var (requestId, inspector, _) = StartedJob();
        api.SeedAs(db =>
        {
            var request = db.Requests.Single(value => value.Id == requestId);
            db.Entry(request).Collection(value => value.History).Load();
            request.TransitionTo(JobStatus.CertificateSubmitted, inspector.Id);
        });
        var client = await SignedInAs(inspector.Email);

        var locked = await client.PostAsJsonAsync($"/api/v1/quotation-queue/{requestId}/reports", Body("a.pdf", "%PDF-1"u8.ToArray()));

        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Contains("reports_locked", await locked.Content.ReadAsStringAsync());
    }
}
