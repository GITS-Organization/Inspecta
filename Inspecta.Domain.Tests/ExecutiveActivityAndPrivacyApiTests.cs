using System.Net;
using System.Text.Json;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The activity feed over HTTP, and the rule that nothing inside a request reaches the general manager.</summary>
public sealed class ExecutiveActivityAndPrivacyApiTests(ApiHttpTests.Api api) : ExecutiveApiTestBase(api), IClassFixture<ApiHttpTests.Api>
{
    private static readonly string[] ForbiddenNames =
        ["subject", "location", "accessNotes", "emailFrom", "emailFromName", "emailPreview", "notes", "answers", "scope", "message", "body", "text", "issueDescription"];

    private static void CollectNames(JsonElement element, ISet<string> names)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    names.Add(property.Name);
                    CollectNames(property.Value, names);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) CollectNames(item, names);
                break;
        }
    }

    [Fact]
    public async Task The_feed_has_coded_events_a_kind_filter_a_cursor_and_the_upcoming_week()
    {
        var world = SeedCompany();
        var client = SeedClient(world, "Delta Steel");
        var request = InspectionRequest.Create("Delta Steel", "Site", client, [RequestItem.Create("Crane", 2)], world.Coordinator.Id, managerId: world.Company.Id);
        foreach (var status in new[] { JobStatus.QuoteDraft, JobStatus.QuoteSent, JobStatus.Unassigned }) request.TransitionTo(status, world.Coordinator.Id);
        request.AssignInspector(world.Inspector.Id, DateTimeOffset.UtcNow.AddDays(2), world.Coordinator.Id);
        Api.SeedAs(db => db.Requests.Add(request));
        SeedInvoice(world, 640_000, ageDays: 1, payments: [(0, 100_000)], clientId: client, clientName: "Delta Steel");
        var gm = await SignedInAs(world.General.Email);

        var feed = await Json(await gm.GetAsync("/api/v1/executive/activity"));
        var quotes = await Json(await gm.GetAsync("/api/v1/executive/activity?kind=quote"));
        var firstPage = await Json(await gm.GetAsync("/api/v1/executive/activity?limit=2"));
        var cursor = firstPage.GetProperty("nextBefore").GetString()!;
        var secondPage = await Json(await gm.GetAsync($"/api/v1/executive/activity?limit=2&before={Uri.EscapeDataString(cursor)}"));

        var codes = feed.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("code").GetString()).ToList();
        Assert.Contains("quotation_sent", codes);
        Assert.Contains("quotation_accepted", codes);
        Assert.Contains("inspector_assigned", codes);
        Assert.Contains("payment_recorded", codes);
        Assert.Contains("invoice_created", codes);
        Assert.Contains("client_added", codes);
        Assert.All(quotes.GetProperty("items").EnumerateArray(), item => Assert.Equal("quote", item.GetProperty("kind").GetString()));
        Assert.Equal(2, firstPage.GetProperty("items").GetArrayLength());
        Assert.NotEqual(firstPage.GetProperty("items")[0].GetProperty("at").GetString(), secondPage.GetProperty("items")[0].GetProperty("at").GetString());
        Assert.Contains(feed.GetProperty("upcoming").EnumerateArray(), item => item.GetProperty("kind").GetString() == "inspection" && item.GetProperty("count").GetInt32() == 2);
        Assert.Equal(HttpStatusCode.BadRequest, (await gm.GetAsync("/api/v1/executive/activity?kind=everything")).StatusCode);
    }

    [Fact]
    public async Task One_company_never_sees_the_activity_of_another_and_only_the_gm_and_owner_may_ask()
    {
        var first = SeedCompany();
        var second = SeedCompany();
        SeedClient(second, "Their Client");
        var gm = await SignedInAs(first.General.Email);

        var feed = await Json(await gm.GetAsync("/api/v1/executive/activity"));

        Assert.DoesNotContain(feed.GetProperty("items").EnumerateArray(), item => item.GetProperty("clientName").GetString() == "Their Client");
        foreach (var email in new[] { first.Coordinator.Email, first.Finance.Email, first.CompanyAdmin.Email })
            Assert.Equal(HttpStatusCode.Forbidden, (await (await SignedInAs(email)).GetAsync("/api/v1/executive/activity")).StatusCode);
    }

    [Fact]
    public async Task No_answer_for_the_general_manager_carries_what_is_inside_a_request()
    {
        var world = SeedCompany();
        var client = SeedClient(world, "Private Steel", location: "Cairo");
        var request = InspectionRequest.Create("Private Steel", "Secret site address", client, [RequestItem.Create("Crane", 1, issueDescription: "secret crack in the boom")], world.Coordinator.Id,
            managerId: world.Company.Id, subject: "Private subject line", accessNotes: "gate code 1234", emailFrom: "secret@private.example");
        request.UseReviewer(world.Manager.Id);
        var quote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(10), world.Coordinator.Id, clientName: "Private Steel");
        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, 800_000, "Per Visit", "Crane inspection")], "private message to the client", null,
            notes: ["private commercial note"], scopeOfWork: ["private scope of work"]);
        quote.Send();
        quote.RecordClientDecision(QuoteStatus.Accepted);
        request.TransitionTo(JobStatus.QuoteDraft, world.Coordinator.Id);
        var invoice = Invoice.CreateFor(quote, request, world.Coordinator.Id, Today.AddDays(-70))!;
        Api.SeedAs(db =>
        {
            db.Requests.Add(request);
            db.Quotes.Add(quote);
            db.Invoices.Add(invoice);
            db.SaveChanges();
            db.Entry(invoice).Property(nameof(Invoice.CreatedAt)).CurrentValue = new DateTimeOffset(Today.AddDays(-70).ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(2));
            db.SaveChanges();
        });
        SeedMachine(client, -3);
        var gm = await SignedInAs(world.General.Email);

        var urls = new[]
        {
            "/api/v1/executive/today", "/api/v1/executive/money", "/api/v1/executive/clients", $"/api/v1/executive/clients/{client}", "/api/v1/executive/renewals",
            "/api/v1/executive/work", "/api/v1/executive/activity", "/api/v1/invoices", $"/api/v1/invoices/{invoice.Id}"
        };
        foreach (var url in urls)
        {
            var response = await gm.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            foreach (var secret in new[] { "Private subject line", "Secret site address", "gate code 1234", "secret@private.example", "secret crack", "private message", "private commercial note", "private scope" })
                Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectNames(JsonDocument.Parse(text).RootElement, names);
            Assert.True(!names.Overlaps(ForbiddenNames), $"{url} has a field a request owns: {string.Join(", ", names.Intersect(ForbiddenNames, StringComparer.OrdinalIgnoreCase))}");
        }
    }
}
