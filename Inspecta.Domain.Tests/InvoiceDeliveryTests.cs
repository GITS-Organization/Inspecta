using Inspecta.Application.Authorization;
using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf;
using Inspecta.Application.Pdf.Models;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Domain.Exceptions;

namespace Inspecta.Domain.Tests;

/// <summary>The invoice email: what it carries, who it goes to, that a failed send changes nothing, and that it waits for the delivery of the certificates.</summary>
public sealed class InvoiceDeliveryTests
{
    private static readonly Guid ManagerId = Guid.NewGuid();
    private static readonly AppUser Coordinator = AppUser.Create("coordinator@example.com", "Eng. Ahmed", UserRole.Coordinator, managerId: ManagerId);
    private static readonly AppUser Inspector = AppUser.Create("inspector@example.com", "Eng. Sara", UserRole.Inspector, managerId: ManagerId);
    private static readonly DateOnly Today = InvoiceCalendar.Today();

    private sealed class RecordingPdf : IPdfGenerator
    {
        public InvoicePdfModel? LastModel { get; private set; }

        public PdfFile Generate<TModel>(TModel model)
        {
            LastModel = model as InvoicePdfModel;
            return new PdfFile([1, 2, 3], $"invoice-{LastModel?.Number}.pdf");
        }
    }

    private sealed class Scene
    {
        public Scene(bool delivered = true, bool mailbox = true, string? clientEmail = null)
        {
            Request = InspectionRequest.CreateFromEmail(ManagerId, Coordinator.Id, new IncomingEmail(
                "42", 1, "Please quote.", null, "buyer@alnoor.com", "Al Noor", "Inspection of our crane",
                [], [], [], [], [], "<id@alnoor.com>", null, null, DateTimeOffset.UtcNow.AddMinutes(-5)));
            Request.UpdateDetails("Al Noor Steel", "Industrial City", null, null, null);
            var crane = Request.AddItem(RequestItem.Create("Mobile crane", 1, type: "Mobile crane"));
            var quote = Quote.Create(Request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(10), Coordinator.Id, clientName: "Al Noor Steel");
            quote.SetDraftDetails([QuoteItem.Create(crane.Id, "Mobile crane", 1, 500_000, "Per Visit", "Mobile crane")], null, null);
            quote.Send();
            quote.RecordClientDecision(QuoteStatus.Accepted);
            Invoice = Invoice.CreateFor(quote, Request, Coordinator.Id, Today.AddDays(-1))!;
            Invoice.RecordPayment(100_000, Today, null, Coordinator.Id, Today);
            if (delivered)
            {
                Request.TransitionTo(JobStatus.QuoteDraft, Coordinator.Id);
                Request.TransitionTo(JobStatus.QuoteSent, Coordinator.Id);
                Request.TransitionTo(JobStatus.Unassigned, Coordinator.Id);
                Request.AssignInspector(Inspector.Id, DateTimeOffset.UtcNow.AddDays(1), Coordinator.Id);
                Request.TransitionTo(JobStatus.TaskAccepted, Inspector.Id);
                Request.TransitionTo(JobStatus.InProgress, Inspector.Id);
                Request.TransitionTo(JobStatus.CertificateSubmitted, Inspector.Id);
                Request.TransitionTo(JobStatus.CertificateApproved, Coordinator.Id);
                Request.TransitionTo(JobStatus.SentToClient, Coordinator.Id);
            }

            Requests = new QuotationTests.FakeRequests(Request);
            Invoices.Items.Add(Invoice);
            var users = new IncomingEmailRequestTests.FakeUserRepository([Coordinator, Inspector]);
            var config = mailbox
                ? CoordinatorEmailConfig.Create(Coordinator.Id, ManagerId, "imap.example.com", 993, "coordinator@example.com", "encrypted", true)
                : null;
            Delivery = new InvoiceDeliveryService(
                Requests, new QuotationTests.FakeConfigs(config), users, new QuotationTests.FakeProtector(), Pdf, Sender);
            Service = new InvoiceService(Invoices, Requests, new QuotationTests.FakeQuotes(), users, Delivery, Pdf);
            _ = clientEmail;
        }

        public InspectionRequest Request { get; }
        public Invoice Invoice { get; }
        public QuotationTests.FakeRequests Requests { get; }
        public FakeInvoices Invoices { get; } = new();
        public RecordingPdf Pdf { get; } = new();
        public QuotationTests.FakeSender Sender { get; } = new();
        public InvoiceDeliveryService Delivery { get; }
        public InvoiceService Service { get; }
    }

    [Fact]
    public async Task The_plan_goes_to_the_sender_of_the_original_email_with_the_invoice_pdf_and_what_is_left_to_pay()
    {
        var scene = new Scene();

        var plan = await scene.Delivery.PlanAsync(scene.Invoice, scene.Request, null);

        Assert.Equal("buyer@alnoor.com", plan.ToAddress);
        Assert.Contains(scene.Invoice.Number, plan.Subject);
        Assert.Contains("Al Noor Steel", plan.Subject);
        Assert.Equal($"invoice-{scene.Invoice.Number}.pdf", plan.AttachmentName);
        Assert.Equal(100_000, scene.Pdf.LastModel!.PaidPiastres);
        Assert.Equal(400_000, scene.Pdf.LastModel.BalancePiastres);
        Assert.Contains("5,000.00 EGP", plan.Body);
        Assert.Empty(plan.Blockers);
    }

    [Fact]
    public async Task An_address_typed_by_the_coordinator_wins_and_the_subject_and_text_can_be_changed()
    {
        var scene = new Scene();

        var plan = await scene.Delivery.PlanAsync(scene.Invoice, scene.Request, new QuoteEmailDraft("accounts@alnoor.com", ["boss@alnoor.com"], "Our invoice", "Dears, here it is."));

        Assert.Equal("accounts@alnoor.com", plan.ToAddress);
        Assert.Equal(["boss@alnoor.com"], plan.Cc);
        Assert.Equal("Our invoice", plan.Subject);
        Assert.Equal("Dears, here it is.", plan.Body);
    }

    [Fact]
    public async Task Without_a_configured_mailbox_the_plan_says_why_it_cannot_be_sent()
    {
        var scene = new Scene(mailbox: false);

        var plan = await scene.Delivery.PlanAsync(scene.Invoice, scene.Request, null);

        Assert.Contains(plan.Blockers, blocker => blocker.Contains("mailbox", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Sending_after_delivery_emails_the_pdf_marks_the_invoice_emailed_and_adds_an_event_each_time()
    {
        var scene = new Scene();

        await scene.Service.SendEmailAsync(scene.Invoice.Id, null, Coordinator.Id, RoleNames.Coordinator);
        await scene.Service.SendEmailAsync(scene.Invoice.Id, null, Coordinator.Id, RoleNames.Coordinator);

        Assert.Equal(2, scene.Sender.Sent.Count);
        var sent = scene.Sender.Sent[0];
        Assert.Equal("buyer@alnoor.com", sent.ToAddress);
        Assert.Equal($"invoice-{scene.Invoice.Number}.pdf", sent.AttachmentFileName);
        Assert.NotNull(scene.Invoice.ClientEmailedAt);
        Assert.Equal(2, scene.Invoice.Events.Count(item => item.Kind == InvoiceEventKind.Emailed));
        Assert.Equal(InvoiceStage.Open, scene.Invoice.Stage);
    }

    [Fact]
    public async Task A_failed_send_changes_nothing_and_can_be_retried()
    {
        var scene = new Scene();
        scene.Sender.Failure = new EmailDeliveryException("The mail server refused it.");

        await Assert.ThrowsAsync<EmailDeliveryException>(() => scene.Service.SendEmailAsync(scene.Invoice.Id, null, Coordinator.Id, RoleNames.Coordinator));

        Assert.Null(scene.Invoice.ClientEmailedAt);
        Assert.DoesNotContain(scene.Invoice.Events, item => item.Kind == InvoiceEventKind.Emailed);
        scene.Sender.Failure = null;
        await scene.Service.SendEmailAsync(scene.Invoice.Id, null, Coordinator.Id, RoleNames.Coordinator);
        Assert.NotNull(scene.Invoice.ClientEmailedAt);
    }

    [Fact]
    public async Task Before_the_certificates_are_delivered_nothing_is_sent()
    {
        var scene = new Scene(delivered: false);

        var error = await Assert.ThrowsAsync<InvoiceRuleException>(() => scene.Service.SendEmailAsync(scene.Invoice.Id, null, Coordinator.Id, RoleNames.Coordinator));

        Assert.Equal("not_delivered", error.Code);
        Assert.Empty(scene.Sender.Sent);
        var preview = await scene.Service.PreviewEmailAsync(scene.Invoice.Id, null, Coordinator.Id, RoleNames.Coordinator);
        Assert.False(preview.CanSend);
        Assert.Contains(preview.Blockers, blocker => blocker.Contains("delivered", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_closed_invoice_is_not_emailed_and_only_the_owning_coordinator_or_the_platform_owner_may_send()
    {
        var scene = new Scene();
        var stranger = AppUser.Create("other@example.com", "Other", UserRole.Coordinator, managerId: ManagerId);

        var forbidden = await Assert.ThrowsAsync<KeyNotFoundException>(() => scene.Service.SendEmailAsync(scene.Invoice.Id, null, stranger.Id, RoleNames.Coordinator));
        var finance = await Assert.ThrowsAsync<AuthException>(() => scene.Service.SendEmailAsync(scene.Invoice.Id, null, Guid.NewGuid(), RoleNames.FinancialManager));

        Assert.NotNull(forbidden);
        Assert.Equal(403, finance.StatusCode);
        scene.Invoice.CloseManually(Coordinator.Id, "Declined");
        var closed = await Assert.ThrowsAsync<InvoiceRuleException>(() => scene.Service.SendEmailAsync(scene.Invoice.Id, null, Coordinator.Id, RoleNames.Coordinator));
        Assert.Equal("invoice_closed", closed.Code);
        Assert.Empty(scene.Sender.Sent);
    }

    [Fact]
    public async Task The_invoice_pdf_is_generated_from_the_frozen_numbers_for_anyone_who_may_view()
    {
        var scene = new Scene();

        var file = await scene.Service.GetPdfAsync(scene.Invoice.Id, Guid.NewGuid(), RoleNames.FinancialManager);

        Assert.Equal($"invoice-{scene.Invoice.Number}.pdf", file.FileName);
        Assert.Equal(500_000, scene.Pdf.LastModel!.TotalPiastres);
    }

    [Fact]
    public async Task The_edit_form_uses_the_same_email_request_as_a_quotation()
    {
        var scene = new Scene();

        var preview = await scene.Service.PreviewEmailAsync(
            scene.Invoice.Id, new SendQuoteEmailRequest { SentTo = "accounts@alnoor.com" }, Coordinator.Id, RoleNames.Coordinator);

        Assert.Equal("invoice", preview.Action);
        Assert.Equal("accounts@alnoor.com", preview.To);
        Assert.True(preview.CanSend);
        Assert.Equal($"/api/v1/invoices/{scene.Invoice.Id}/pdf", preview.PdfUrl);
    }
}
