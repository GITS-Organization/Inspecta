using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>Invoices over HTTP: how one is made, who records payments when, the hand-over, the close, and who sees what.</summary>
public sealed class InvoiceApiTests : IClassFixture<ApiHttpTests.Api>
{
    private static int caller = 120;
    private static int sequence;
    private readonly ApiHttpTests.Api api;

    public InvoiceApiTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded();
    }

    private static DateOnly Today => InvoiceCalendar.Today();

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.77.88." + Interlocked.Increment(ref caller));
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = ApiHttpTests.Password });
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"login of {email} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString()!);
        return client;
    }

    private sealed record Setup(
        Company Company, AppUser Coordinator, AppUser OtherCoordinator, AppUser Finance, AppUser General, AppUser Inspector, AppUser Manager, AppUser CompanyAdmin);

    private Setup SeedCompany()
    {
        var number = Interlocked.Increment(ref sequence);
        var company = Company.Create($"Invoices {number}");
        api.SeedAs(db => db.Companies.Add(company));
        var manager = api.AddUser($"inv-tm-{number}@example.com", UserRole.TechnicalManager, company.Id);
        api.SeedAs(db => db.Companies.Single(item => item.Id == company.Id).SetDefaultReviewer(manager.Id));
        return new Setup(
            company,
            api.AddUser($"inv-coordinator-{number}@example.com", UserRole.Coordinator, company.Id),
            api.AddUser($"inv-other-{number}@example.com", UserRole.Coordinator, company.Id),
            api.AddUser($"inv-fm-{number}@example.com", UserRole.FinancialManager, company.Id),
            api.AddUser($"inv-gm-{number}@example.com", UserRole.GeneralManager, company.Id),
            api.AddUser($"inv-inspector-{number}@example.com", UserRole.Inspector, company.Id),
            manager,
            api.AddUser($"inv-admin-{number}@example.com", UserRole.CompanyAdmin, company.Id));
    }

    private static void Deliver(InspectionRequest request, Setup setup)
    {
        request.TransitionTo(JobStatus.QuoteDraft, setup.Coordinator.Id);
        request.TransitionTo(JobStatus.QuoteSent, setup.Coordinator.Id);
        request.TransitionTo(JobStatus.Unassigned, setup.Coordinator.Id);
        request.AssignInspector(setup.Inspector.Id, DateTimeOffset.UtcNow.AddDays(1), setup.Coordinator.Id);
        request.TransitionTo(JobStatus.TaskAccepted, setup.Inspector.Id);
        request.TransitionTo(JobStatus.InProgress, setup.Inspector.Id);
        request.TransitionTo(JobStatus.CertificateSubmitted, setup.Inspector.Id);
        request.TransitionTo(JobStatus.CertificateApproved, setup.Manager.Id);
        request.TransitionTo(JobStatus.SentToClient, setup.Coordinator.Id);
    }

    /// <summary>An invoice for a request of the company, accepted yesterday so that payments dated today are valid.</summary>
    private Invoice SeedInvoice(Setup setup, bool delivered = false, bool emailRequest = false, long price = 1_000_000, Guid? coordinatorId = null)
    {
        var owner = coordinatorId ?? setup.Coordinator.Id;
        var request = emailRequest
            ? InspectionRequest.Create("Mail client", "Cairo", null, [RequestItem.Create("Crane", 1)], owner, managerId: setup.Company.Id,
                source: RequestSource.Email, emailFrom: "buyer@example.com")
            : InspectionRequest.Create($"Client {Interlocked.Increment(ref sequence)}", "Cairo", null, [RequestItem.Create("Crane", 1)], owner, managerId: setup.Company.Id);
        var quote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(10), owner, clientName: request.ClientName);
        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, price, "Per Visit", "Crane inspection")], null, null);
        quote.Send();
        quote.RecordClientDecision(QuoteStatus.Accepted);
        var invoice = Invoice.CreateFor(quote, request, owner, Today.AddDays(-1))!;
        if (delivered) Deliver(request, setup);
        api.SeedAs(db =>
        {
            db.Requests.Add(request);
            db.Quotes.Add(quote);
            db.Invoices.Add(invoice);
        });
        return invoice;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await Json(response)).TryGetProperty("code", out var code) ? code.GetString() : null;

    private static object Payment(long amount, string? label = null, DateOnly? paidOn = null) => new { amountPiastres = amount, paidOn = (paidOn ?? Today).ToString("yyyy-MM-dd"), label };

    private static long Balance(JsonElement detail) => detail.GetProperty("summary").GetProperty("balancePiastres").GetInt64();

    [Fact]
    public async Task Accepting_a_quote_creates_the_invoice()
    {
        var setup = SeedCompany();
        var request = InspectionRequest.Create("Fresh client", "Cairo", null, [RequestItem.Create("Crane", 1)], setup.Coordinator.Id, managerId: setup.Company.Id);
        request.TransitionTo(JobStatus.QuoteDraft, setup.Coordinator.Id);
        request.TransitionTo(JobStatus.QuoteSent, setup.Coordinator.Id);
        var quote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(10), setup.Coordinator.Id, clientName: "Fresh client");
        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, 1_000_000, "Per Visit")], null, null);
        quote.SetVat(14m);
        quote.Send();
        api.SeedAs(db => { db.Requests.Add(request); db.Quotes.Add(quote); });
        var coordinator = await SignedInAs(setup.Coordinator.Email);

        var accepted = await coordinator.PostAsJsonAsync($"/api/v1/requests/{request.Id}/quotes/{quote.Id}/decision", new { decision = "accepted" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var list = await Json(await coordinator.GetAsync($"/api/v1/invoices?requestId={request.Id}"));
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        var invoice = list.GetProperty("items")[0];
        Assert.Equal(1_140_000, invoice.GetProperty("totalPiastres").GetInt64());
        Assert.Equal(1_140_000, invoice.GetProperty("balancePiastres").GetInt64());
        Assert.Equal("unpaid", invoice.GetProperty("paymentState").GetString());
        Assert.Equal("open", invoice.GetProperty("stage").GetString());

        var again = await coordinator.PostAsJsonAsync($"/api/v1/requests/{request.Id}/quotes/{quote.Id}/decision", new { decision = "accepted" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(1, (await Json(await coordinator.GetAsync($"/api/v1/invoices?requestId={request.Id}"))).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Payments_accumulate_and_show_who_and_when()
    {
        var setup = SeedCompany();
        var invoice = SeedInvoice(setup);
        var coordinator = await SignedInAs(setup.Coordinator.Email);

        var first = await coordinator.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", Payment(200_000, "Advance"));
        var second = await coordinator.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", Payment(500_000));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var detail = await Json(second);
        Assert.Equal(300_000, Balance(detail));
        Assert.Equal("partial", detail.GetProperty("summary").GetProperty("paymentState").GetString());
        Assert.Equal(2, detail.GetProperty("payments").GetArrayLength());
        var row = detail.GetProperty("payments")[0];
        Assert.Equal(setup.Coordinator.Id, row.GetProperty("recordedBy").GetGuid());
        Assert.Equal(setup.Coordinator.FullName, row.GetProperty("recordedByName").GetString());
        Assert.Equal("Advance", row.GetProperty("label").GetString());
        var history = detail.GetProperty("history").EnumerateArray().Select(item => item.GetProperty("kind").GetString()).ToArray();
        Assert.Equal(["created", "payment", "payment"], history);
        Assert.Contains("record_payment", detail.GetProperty("actions").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task A_payment_over_the_balance_is_refused_with_overpayment()
    {
        var setup = SeedCompany();
        var invoice = SeedInvoice(setup, price: 1_000);
        var coordinator = await SignedInAs(setup.Coordinator.Email);

        var response = await coordinator.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", Payment(1_001));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("overpayment", await CodeOf(response));
        Assert.Equal(1_000, Balance(await Json(await coordinator.GetAsync($"/api/v1/invoices/{invoice.Id}"))));
    }

    [Theory]
    [InlineData(0, 0, "payment_amount_invalid")]
    [InlineData(100, 1, "payment_date_invalid")]
    [InlineData(100, -3, "payment_date_invalid")]
    public async Task A_bad_payment_is_a_400_with_its_code(long amount, int dayOffset, string code)
    {
        var setup = SeedCompany();
        var invoice = SeedInvoice(setup);
        var coordinator = await SignedInAs(setup.Coordinator.Email);

        var response = await coordinator.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", Payment(amount, paidOn: Today.AddDays(dayOffset)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await CodeOf(response));
    }

    [Fact]
    public async Task A_reversal_keeps_both_rows_and_brings_the_balance_back()
    {
        var setup = SeedCompany();
        var invoice = SeedInvoice(setup);
        var coordinator = await SignedInAs(setup.Coordinator.Email);
        var paid = await Json(await coordinator.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", Payment(200_000)));
        var paymentId = paid.GetProperty("payments")[0].GetProperty("id").GetGuid();

        var noReason = await coordinator.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments/{paymentId}/reverse", new { reason = " " });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        var reversed = await coordinator.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments/{paymentId}/reverse", new { reason = "Wrong client" });

        var detail = await Json(reversed);
        Assert.Equal(1_000_000, Balance(detail));
        Assert.Equal(2, detail.GetProperty("payments").GetArrayLength());
        Assert.Equal(-200_000, detail.GetProperty("payments")[1].GetProperty("amountPiastres").GetInt64());
        var again = await coordinator.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments/{paymentId}/reverse", new { reason = "Again" });
        Assert.Equal("already_reversed", await CodeOf(again));
    }

    [Fact]
    public async Task Hand_over_before_the_certificates_are_delivered_is_refused()
    {
        var setup = SeedCompany();
        var invoice = SeedInvoice(setup, delivered: false);
        var coordinator = await SignedInAs(setup.Coordinator.Email);

        var response = await coordinator.PostAsync($"/api/v1/invoices/{invoice.Id}/hand-over", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("not_delivered", await CodeOf(response));
    }

    [Fact]
    public async Task After_the_hand_over_the_coordinator_cannot_record_but_the_financial_manager_can()
    {
        var setup = SeedCompany();
        var invoice = SeedInvoice(setup, delivered: true);
        var coordinator = await SignedInAs(setup.Coordinator.Email);
        var finance = await SignedInAs(setup.Finance.Email);
        Assert.Equal(HttpStatusCode.Conflict, (await finance.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", Payment(100))).StatusCode);

        var handed = await coordinator.PostAsync($"/api/v1/invoices/{invoice.Id}/hand-over", null);

        Assert.Equal(HttpStatusCode.OK, handed.StatusCode);
        Assert.Equal("with_finance", (await Json(handed)).GetProperty("summary").GetProperty("stage").GetString());
        var refused = await coordinator.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", Payment(100));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("wrong_stage", await CodeOf(refused));
        Assert.Equal(HttpStatusCode.OK, (await finance.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", Payment(100))).StatusCode);
        Assert.Equal("wrong_stage", await CodeOf(await coordinator.PostAsync($"/api/v1/invoices/{invoice.Id}/hand-over", null)));
    }

    [Fact]
    public async Task The_financial_manager_closes_only_at_a_zero_balance_and_a_closed_invoice_accepts_nothing()
    {
        var setup = SeedCompany();
        var invoice = SeedInvoice(setup, delivered: true, price: 1_000);
        var coordinator = await SignedInAs(setup.Coordinator.Email);
        var finance = await SignedInAs(setup.Finance.Email);
        await coordinator.PostAsync($"/api/v1/invoices/{invoice.Id}/hand-over", null);

        var early = await finance.PostAsync($"/api/v1/invoices/{invoice.Id}/close", null);
        Assert.Equal("balance_not_zero", await CodeOf(early));
        await finance.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", Payment(1_000));
        var closed = await finance.PostAsync($"/api/v1/invoices/{invoice.Id}/close", null);

        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var detail = await Json(closed);
        Assert.Equal("closed", detail.GetProperty("summary").GetProperty("stage").GetString());
        Assert.Equal("paid", detail.GetProperty("summary").GetProperty("paymentState").GetString());
        Assert.Empty(detail.GetProperty("actions").EnumerateArray());
        var after = await finance.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", Payment(1));
        Assert.Equal("invoice_closed", await CodeOf(after));
    }

    [Fact]
    public async Task The_financial_manager_can_close_by_hand_with_a_reason_whatever_is_owed()
    {
        var setup = SeedCompany();
        var invoice = SeedInvoice(setup, delivered: true);
        var coordinator = await SignedInAs(setup.Coordinator.Email);
        var finance = await SignedInAs(setup.Finance.Email);
        await coordinator.PostAsync($"/api/v1/invoices/{invoice.Id}/hand-over", null);
        await finance.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", Payment(200_000));

        var closed = await finance.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/close", new { reason = "Job declined; advance settled" });

        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        var detail = await Json(closed);
        Assert.Equal("closed", detail.GetProperty("summary").GetProperty("stage").GetString());
        Assert.Equal(800_000, Balance(detail));
        Assert.Contains(detail.GetProperty("history").EnumerateArray(), item => item.GetProperty("note").GetString() == "Job declined; advance settled");
    }

    [Fact]
    public async Task The_financial_and_general_manager_see_invoices_but_no_request_details_and_cannot_open_the_request()
    {
        var setup = SeedCompany();
        var invoice = SeedInvoice(setup);
        var requestId = invoice.RequestId;
        foreach (var email in new[] { setup.Finance.Email, setup.General.Email })
        {
            var user = await SignedInAs(email);

            var list = await Json(await user.GetAsync("/api/v1/invoices"));
            Assert.Contains(list.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == invoice.Id);
            var text = await (await user.GetAsync($"/api/v1/invoices/{invoice.Id}")).Content.ReadAsStringAsync();
            foreach (var forbidden in new[] { "inspector", "equipment", "certificate", "location", "accessNotes", "serial" })
                Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"/api/v1/requests/{requestId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"/api/v1/requests/{requestId}/quotes")).StatusCode);
        }
    }

    [Fact]
    public async Task The_general_manager_only_looks()
    {
        var setup = SeedCompany();
        var invoice = SeedInvoice(setup, delivered: true);
        var general = await SignedInAs(setup.General.Email);

        Assert.Equal(HttpStatusCode.Forbidden, (await general.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", Payment(1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await general.PostAsync($"/api/v1/invoices/{invoice.Id}/hand-over", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await general.PostAsync($"/api/v1/invoices/{invoice.Id}/close", null)).StatusCode);
        var detail = await Json(await general.GetAsync($"/api/v1/invoices/{invoice.Id}"));
        Assert.Empty(detail.GetProperty("actions").EnumerateArray());
    }

    [Fact]
    public async Task Technical_manager_inspector_and_company_admin_are_forbidden()
    {
        var setup = SeedCompany();
        var invoice = SeedInvoice(setup);
        foreach (var email in new[] { setup.Manager.Email, setup.Inspector.Email, setup.CompanyAdmin.Email })
        {
            var user = await SignedInAs(email);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/invoices")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"/api/v1/invoices/{invoice.Id}")).StatusCode);
        }
    }

    [Fact]
    public async Task Another_coordinators_email_request_invoice_is_hidden_and_untouchable()
    {
        var setup = SeedCompany();
        var invoice = SeedInvoice(setup, emailRequest: true);
        var other = await SignedInAs(setup.OtherCoordinator.Email);

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/invoices/{invoice.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", Payment(1))).StatusCode);
        var list = await Json(await other.GetAsync("/api/v1/invoices"));
        Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == invoice.Id);
        var owner = await SignedInAs(setup.Coordinator.Email);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/invoices/{invoice.Id}")).StatusCode);
    }

    [Fact]
    public async Task Another_company_can_never_see_or_touch_the_invoices()
    {
        var setup = SeedCompany();
        var stranger = SeedCompany();
        var invoice = SeedInvoice(setup, delivered: true);
        foreach (var email in new[] { stranger.Coordinator.Email, stranger.Finance.Email, stranger.General.Email })
        {
            var user = await SignedInAs(email);

            Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync($"/api/v1/invoices/{invoice.Id}")).StatusCode);
            if (email != stranger.General.Email)
                Assert.Equal(HttpStatusCode.NotFound, (await user.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", Payment(1))).StatusCode);
            if (email == stranger.Coordinator.Email)
                Assert.Equal(HttpStatusCode.NotFound, (await user.PostAsync($"/api/v1/invoices/{invoice.Id}/hand-over", null)).StatusCode);
            if (email == stranger.Finance.Email)
                Assert.Equal(HttpStatusCode.NotFound, (await user.PostAsync($"/api/v1/invoices/{invoice.Id}/close", null)).StatusCode);
            var list = await Json(await user.GetAsync("/api/v1/invoices"));
            Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == invoice.Id);
        }
    }

    [Fact]
    public async Task The_list_filters_by_stage_and_payment_state()
    {
        var setup = SeedCompany();
        var open = SeedInvoice(setup, price: 1_000);
        var paid = SeedInvoice(setup, price: 1_000);
        var coordinator = await SignedInAs(setup.Coordinator.Email);
        await coordinator.PostAsJsonAsync($"/api/v1/invoices/{paid.Id}/payments", Payment(1_000));

        var unpaid = await Json(await coordinator.GetAsync("/api/v1/invoices?paymentState=unpaid"));
        var paidList = await Json(await coordinator.GetAsync("/api/v1/invoices?paymentState=paid&stage=open"));
        var bad = await coordinator.GetAsync("/api/v1/invoices?stage=sideways");

        Assert.Equal([open.Id], unpaid.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
        Assert.Equal([paid.Id], paidList.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task An_invoice_can_be_made_for_a_job_accepted_before_invoices_existed_and_only_once()
    {
        var setup = SeedCompany();
        var request = InspectionRequest.Create("Old job", "Cairo", null, [RequestItem.Create("Crane", 1)], setup.Coordinator.Id, managerId: setup.Company.Id);
        var quote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(10), setup.Coordinator.Id, clientName: "Old job");
        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, 50_000, "Per Visit")], null, null);
        quote.Send();
        quote.RecordClientDecision(QuoteStatus.Accepted);
        api.SeedAs(db => { db.Requests.Add(request); db.Quotes.Add(quote); });
        var coordinator = await SignedInAs(setup.Coordinator.Email);

        var made = await coordinator.PostAsync($"/api/v1/requests/{request.Id}/invoice", null);
        var again = await coordinator.PostAsync($"/api/v1/requests/{request.Id}/invoice", null);

        Assert.Equal(HttpStatusCode.OK, made.StatusCode);
        var first = await Json(made);
        Assert.Equal(50_000, first.GetProperty("summary").GetProperty("totalPiastres").GetInt64());
        Assert.Equal(first.GetProperty("summary").GetProperty("id").GetGuid(), (await Json(again)).GetProperty("summary").GetProperty("id").GetGuid());
        Assert.Equal(1, (await Json(await coordinator.GetAsync($"/api/v1/invoices?requestId={request.Id}"))).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task A_job_without_a_price_or_without_an_accepted_quote_needs_no_invoice()
    {
        var setup = SeedCompany();
        var free = InspectionRequest.Create("Man day client", "Cairo", null, [RequestItem.Create("Crane", 1)], setup.Coordinator.Id, managerId: setup.Company.Id);
        var placeholder = Quote.CreateAccepted(free.Id, setup.Coordinator.Id, "MD", "Man day client");
        var unquoted = InspectionRequest.Create("No quote", "Cairo", null, [RequestItem.Create("Crane", 1)], setup.Coordinator.Id, managerId: setup.Company.Id);
        api.SeedAs(db => { db.Requests.AddRange(free, unquoted); db.Quotes.Add(placeholder); });
        var coordinator = await SignedInAs(setup.Coordinator.Email);

        var noPrice = await coordinator.PostAsync($"/api/v1/requests/{free.Id}/invoice", null);
        var noQuote = await coordinator.PostAsync($"/api/v1/requests/{unquoted.Id}/invoice", null);

        Assert.Equal("no_invoice_needed", await CodeOf(noPrice));
        Assert.Equal("no_accepted_quote", await CodeOf(noQuote));
        Assert.Equal(0, (await Json(await coordinator.GetAsync($"/api/v1/invoices?requestId={free.Id}"))).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task The_financial_and_general_manager_can_download_the_invoice_pdf()
    {
        var setup = SeedCompany();
        var invoice = SeedInvoice(setup);
        foreach (var email in new[] { setup.Finance.Email, setup.General.Email, setup.Coordinator.Email })
        {
            var user = await SignedInAs(email);

            var response = await user.GetAsync($"/api/v1/invoices/{invoice.Id}/pdf");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString((await response.Content.ReadAsByteArrayAsync())[..5]));
        }
    }

    [Fact]
    public async Task Emailing_the_client_waits_for_the_delivery_and_belongs_to_the_coordinator()
    {
        var setup = SeedCompany();
        var invoice = SeedInvoice(setup, delivered: false);
        var coordinator = await SignedInAs(setup.Coordinator.Email);
        var finance = await SignedInAs(setup.Finance.Email);
        var general = await SignedInAs(setup.General.Email);

        var early = await coordinator.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/email", new { });
        var preview = await Json(await coordinator.GetAsync($"/api/v1/invoices/{invoice.Id}/email"));

        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal("not_delivered", await CodeOf(early));
        Assert.False(preview.GetProperty("canSend").GetBoolean());
        Assert.Contains(preview.GetProperty("blockers").EnumerateArray(), item => item.GetString()!.Contains("delivered"));
        Assert.Equal($"/api/v1/invoices/{invoice.Id}/pdf", preview.GetProperty("pdfUrl").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/email", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await general.GetAsync($"/api/v1/invoices/{invoice.Id}/email")).StatusCode);
    }

    [Fact]
    public async Task The_platform_owner_can_reverse_a_payment_on_a_closed_invoice()
    {
        var setup = SeedCompany();
        var invoice = SeedInvoice(setup, delivered: true, price: 1_000);
        var coordinator = await SignedInAs(setup.Coordinator.Email);
        var finance = await SignedInAs(setup.Finance.Email);
        await coordinator.PostAsync($"/api/v1/invoices/{invoice.Id}/hand-over", null);
        var paid = await Json(await finance.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments", Payment(1_000)));
        await finance.PostAsync($"/api/v1/invoices/{invoice.Id}/close", null);
        var paymentId = paid.GetProperty("payments")[0].GetProperty("id").GetGuid();
        var owner = await SignedInAs(api.AddUser($"inv-owner-{Interlocked.Increment(ref sequence)}@example.com", UserRole.Admin).Email);

        var byFinance = await finance.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments/{paymentId}/reverse", new { reason = "Late fix" });
        var byOwner = await owner.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id}/payments/{paymentId}/reverse", new { reason = "Late fix" });

        Assert.Equal("invoice_closed", await CodeOf(byFinance));
        Assert.Equal(HttpStatusCode.OK, byOwner.StatusCode);
        Assert.Equal(1_000, Balance(await Json(byOwner)));
    }
}
