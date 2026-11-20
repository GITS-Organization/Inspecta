using System.IO.Compression;
using System.Security.Claims;
using Inspecta.Application.Certificates;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Contracts.Clients;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Inspecta.Infrastructure.Pdf;
using Inspecta.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inspecta.Domain.Tests;

/// <summary>Handing the approved certificates of a job to the client, with and without the email.</summary>
public sealed class CertificateDeliveryTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;
    private readonly ServiceProvider pdfServices;
    private readonly QuotationTests.FakeSender sender = new();
    private readonly AppUser manager = AppUser.Create("manager@example.com", "Manager Mona", UserRole.TechnicalManager);
    private readonly AppUser coordinator;
    private readonly AppUser inspector;
    private Guid requestId;
    private Guid clientId;
    private Guid[] itemIds = [];
    private bool microsoftMailbox;
    private IMailboxAuthenticator? authenticator;
    private JobReportOptions? reportOptions;
    private int reportLoads;

    public CertificateDeliveryTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        coordinator = AppUser.Create("coordinator@example.com", "Coordinator Ali", UserRole.Coordinator, managerId: manager.Id);
        inspector = AppUser.Create("inspector@example.com", "Eng. Karim", UserRole.Inspector, managerId: manager.Id);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPdfGeneration(new ConfigurationBuilder().Build());
        pdfServices = services.BuildServiceProvider();

        using var db = CreateContext();
        db.Database.EnsureCreated();
        db.Users.AddRange(manager, coordinator, inspector);
        db.SaveChanges();
    }

    [Fact]
    public async Task WithEmailSwitchedOffTheJobIsHandedOverWithoutSendingAnythingOrFailing()
    {
        await ApprovedJobAsync(fromEmail: true);

        var preview = await Run(false, service => service.PreviewDeliveryAsync(requestId, coordinator.Id, "coordinator"));
        Assert.False(preview.EmailEnabled);
        Assert.Contains("switched off", preview.Notice);
        Assert.Equal("buyer@alnoor.com", preview.To);

        var delivered = await Run(false, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));

        Assert.Equal(JobStatus.SentToClient, delivered.RequestStatus);
        Assert.False(delivered.Delivery!.EmailSent);
        Assert.Contains("switched off", delivered.Delivery.Notice);
        Assert.Empty(sender.Sent);
        await using var db = CreateContext();
        Assert.Empty(db.CertificateDeliveries.ToList());
    }

    [Fact]
    public async Task SendsWithTheAccessTokenForAMicrosoftMailbox()
    {
        microsoftMailbox = true;
        authenticator = new StubMailboxAuthenticator("cert-token");
        await ApprovedJobAsync(fromEmail: true);

        var delivered = await Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));

        Assert.True(delivered.Delivery!.EmailSent);
        Assert.Equal("cert-token", sender.LastSmtp!.AccessToken);
        Assert.Equal(string.Empty, sender.LastSmtp.Password);
        Assert.Equal("smtp.office365.com", sender.LastSmtp.Server);
    }

    [Fact]
    public async Task ASendFromAMailboxThatNeedsReconnectAnswers409AndLeavesNothingStuck()
    {
        microsoftMailbox = true;
        var stub = new StubMailboxAuthenticator { Reject = true };
        authenticator = stub;
        await ApprovedJobAsync(fromEmail: true);

        var failure = await Assert.ThrowsAsync<AuthException>(() =>
            Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest())));

        Assert.Equal("mailbox_reconnect_required", failure.Code);
        Assert.Empty(sender.Sent);
        await using (var db = CreateContext()) Assert.Empty(db.CertificateDeliveries.ToList());

        stub.Reject = false;
        var delivered = await Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));
        Assert.True(delivered.Delivery!.EmailSent);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task FilesAddedByTheCoordinatorGoAfterTheCertificates()
    {
        await ApprovedJobAsync(fromEmail: true);
        var notes = new Application.Contracts.Mail.EmailAttachmentUpload("notes.txt", Convert.ToBase64String("Please keep these."u8.ToArray()));

        var delivered = await Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest { Attachments = [notes] }));

        Assert.True(delivered.Delivery!.EmailSent);
        var email = Assert.Single(sender.Sent);
        Assert.Equal(2, email.MoreAttachments!.Count);
        Assert.Equal("notes.txt", email.MoreAttachments.Last().FileName);
        Assert.EndsWith(".pdf", email.AttachmentFileName);
    }

    [Fact]
    public async Task WithEmailOnTheCertificatesGoAsAReplyToTheOriginalRequestWithEveryPdfAttached()
    {
        await ApprovedJobAsync(fromEmail: true);

        var preview = await Run(true, service => service.PreviewDeliveryAsync(requestId, coordinator.Id, "coordinator"));
        Assert.True(preview.EmailEnabled);
        Assert.Null(preview.Notice);
        Assert.Equal("buyer@alnoor.com", preview.To);
        Assert.True(preview.RepliesToOriginalEmail);
        Assert.Equal("Inspection Certificates - Al Noor Industries", preview.Subject);
        Assert.Contains("Forklift Truck - CERT-", preview.Body);
        Assert.Contains("QR code", preview.Body);
        Assert.Equal(2, preview.Attachments.Count);

        var delivered = await Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));

        Assert.True(delivered.Delivery!.EmailSent);
        Assert.Equal("buyer@alnoor.com", delivered.Delivery.SentTo);
        Assert.Equal(JobStatus.SentToClient, delivered.RequestStatus);
        var email = Assert.Single(sender.Sent);
        Assert.Equal("buyer@alnoor.com", email.ToAddress);
        Assert.Equal("coordinator@example.com", email.FromAddress);
        Assert.Equal("Inspection Certificates - Al Noor Industries", email.Subject);
        Assert.Equal("<id@alnoor.com>", email.InReplyTo);
        Assert.Contains("<id@alnoor.com>", email.References);
        Assert.Equal(2, 1 + email.MoreAttachments!.Count);
        Assert.All(email.MoreAttachments.Select(file => file.Content).Append(email.AttachmentContent), content => Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(content!, 0, 5)));
        await using var db = CreateContext();
        var row = Assert.Single(db.CertificateDeliveries.ToList());
        Assert.True(row.Sent);
        Assert.Equal(email.MessageId, row.MessageId);
    }

    [Fact]
    public async Task AFailedEmailChangesNothingAndRetryingSendsOnlyOneEmailWithTheSameMessageId()
    {
        await ApprovedJobAsync(fromEmail: true);
        sender.Failure = new EmailDeliveryException("The mail server did not answer.");

        await Assert.ThrowsAsync<EmailDeliveryException>(() =>
            Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest())));

        Assert.Equal(JobStatus.CertificateApproved, (await Run(true, service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"))).RequestStatus);
        string failedMessageId;
        await using (var db = CreateContext())
        {
            var failed = Assert.Single(db.CertificateDeliveries.ToList());
            Assert.False(failed.Sent);
            Assert.Contains("did not answer", failed.LastError);
            failedMessageId = failed.MessageId;
        }

        sender.Failure = null;
        var delivered = await Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));

        Assert.True(delivered.Delivery!.EmailSent);
        Assert.Equal(failedMessageId, Assert.Single(sender.Sent).MessageId);
        await using var check = CreateContext();
        var row = Assert.Single(check.CertificateDeliveries.ToList());
        Assert.True(row.Sent);
        Assert.Equal(2, row.Attempts);
    }

    [Fact]
    public async Task AnotherClickWhileTheEmailIsStillGoingOutSendsNothingMore()
    {
        await ApprovedJobAsync(fromEmail: true);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sender.Hold = release.Task;

        var first = Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));
        await sender.Started.Task; // the mail server is busy with the first one

        var second = await Assert.ThrowsAsync<AuthException>(() =>
            Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest())));
        Assert.Equal(("delivery_in_progress", 409), (second.Code, second.StatusCode));

        release.SetResult();
        Assert.True((await first).Delivery!.EmailSent);
        Assert.Single(sender.Sent);

        // And once it is done, a late click finds the job already handed over.
        var late = await Assert.ThrowsAsync<AuthException>(() =>
            Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest())));
        Assert.Equal("not_approved", late.Code);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task TheCoordinatorCanChooseTheRecipientAndWriteTheText()
    {
        await ApprovedJobAsync(fromEmail: true);

        await Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator",
            new DeliverCertificatesRequest { SentTo = "quality@alnoor.com", Message = "Dear team, the certificates are attached." }));

        var email = Assert.Single(sender.Sent);
        Assert.Equal("quality@alnoor.com", email.ToAddress);
        Assert.Equal("Dear team, the certificates are attached.", email.BodyText);
        Assert.Null(email.InReplyTo);
    }

    [Fact]
    public async Task ThePreviewIsAReadyEmailWithTheCertificatesListedAndNothingMissing()
    {
        await ApprovedJobAsync(fromEmail: true);

        var preview = await Run(true, service => service.PreviewDeliveryAsync(requestId, coordinator.Id, "coordinator"));

        Assert.True(preview.CanSend);
        Assert.Empty(preview.Blockers!);
        Assert.False(preview.AlreadyDelivered);
        Assert.Equal("coordinator@example.com", preview.From);
        Assert.Equal("coordinator@example.com", preview.TestEmailTo);
        Assert.Empty(preview.Cc!);
        Assert.StartsWith("Dears,", preview.Body);
        Assert.Contains("1. ", preview.Body);
        Assert.Contains("Satisfactory", preview.Body);
        Assert.Contains("examined 05/11/2026, next examination 05/11/2027", preview.Body);
        Assert.Contains("Check or download: https://api.gitsplatform.com/verify/", preview.Body);
        Assert.Equal(2, preview.Certificates!.Count);
        Assert.All(preview.Certificates, certificate =>
        {
            Assert.StartsWith("CERT-", certificate.CertificateNumber);
            Assert.Equal(InspectionResult.Satisfactory, certificate.Result);
            Assert.Equal(new DateOnly(2027, 11, 5), certificate.NextExaminationDate);
            Assert.StartsWith($"/api/v1/quotation-queue/{requestId}/machines/", certificate.PdfUrl);
            Assert.EndsWith("/pdf", certificate.PdfUrl);
            Assert.True(certificate.SizeBytes > 1000);
            Assert.StartsWith("https://api.gitsplatform.com/verify/", certificate.VerifyUrl);
        });
        Assert.Equal(itemIds.Order(), preview.Certificates.Select(certificate => certificate.RequestItemId!.Value).Order());
    }

    [Fact]
    public async Task TheCoordinatorEditsTheAddressesSubjectAndTextAndTheEmailGoesWithThem()
    {
        await ApprovedJobAsync(fromEmail: true);

        var delivered = await Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest
        {
            SentTo = "quality@alnoor.com",
            Cc = ["boss@alnoor.com", "BOSS@alnoor.com", "quality@alnoor.com", "  "],
            Subject = "Certificates for the October inspection",
            Message = "Dear team,\nSee https://example.com/check?a=1&b=2 for details."
        }));

        var email = Assert.Single(sender.Sent);
        Assert.Equal("quality@alnoor.com", email.ToAddress);
        Assert.Equal(["boss@alnoor.com"], email.Cc!);
        Assert.Equal("Certificates for the October inspection", email.Subject);
        Assert.Equal("Dear team,\nSee https://example.com/check?a=1&b=2 for details.", email.BodyText);
        // The HTML version keeps the text, makes the link clickable and escapes the rest.
        Assert.Contains("<a href=\"https://example.com/check?a=1&amp;b=2\">", email.BodyHtml);
        Assert.DoesNotContain("<script", email.BodyHtml);
        Assert.Equal(["boss@alnoor.com"], delivered.Delivery!.Cc!);
        Assert.Equal("Certificates for the October inspection", delivered.Delivery.Subject);
        Assert.NotNull(delivered.Delivery.SentAt);

        // The job keeps saying what went out, to whom and when.
        var later = await Run(true, service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"));
        Assert.Equal(JobStatus.SentToClient, later.RequestStatus);
        Assert.True(later.Delivery!.EmailSent);
        Assert.Equal("quality@alnoor.com", later.Delivery.SentTo);
        Assert.Equal(["boss@alnoor.com"], later.Delivery.Cc!);
        Assert.Equal("Certificates for the October inspection", later.Delivery.Subject);
        Assert.NotNull(later.Delivery.SentAt);
        await using var db = CreateContext();
        Assert.Equal("boss@alnoor.com", Assert.Single(db.CertificateDeliveries.ToList()).CcAddresses);

        // Delivered: the preview says so and nothing can be sent a second time.
        var preview = await Run(true, service => service.PreviewDeliveryAsync(requestId, coordinator.Id, "coordinator"));
        Assert.True(preview.AlreadyDelivered);
        Assert.False(preview.CanSend);
        Assert.Equal("not_approved", (await Assert.ThrowsAsync<AuthException>(() => Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest())))).Code);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task ABadAddressIsRefusedBeforeAnythingIsSent()
    {
        await ApprovedJobAsync(fromEmail: true);

        foreach (var (request, expected) in new (DeliverCertificatesRequest, string)[]
                 {
                     (new DeliverCertificatesRequest { SentTo = "not an email" }, "is not a valid email address (To)"),
                     (new DeliverCertificatesRequest { SentTo = "a@b.com, c@d.com" }, "is not a valid email address (To)"),
                     (new DeliverCertificatesRequest { SentTo = "a@b.com\r\nBcc: x@y.com" }, "is not a valid email address (To)"),
                     (new DeliverCertificatesRequest { Cc = ["nobody@localhost"] }, "is not a valid email address (Cc)"),
                     (new DeliverCertificatesRequest { Cc = Enumerable.Range(1, 11).Select(index => $"person{index}@alnoor.com").ToArray() }, "At most 10 addresses")
                 })
        {
            var refused = await Assert.ThrowsAsync<ArgumentException>(() => Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", request)));
            Assert.Contains(expected, refused.Message);
        }

        Assert.Empty(sender.Sent);
        Assert.Equal(JobStatus.CertificateApproved, (await Run(true, service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"))).RequestStatus);
        await using var db = CreateContext();
        Assert.Empty(db.CertificateDeliveries.ToList());
    }

    [Fact]
    public async Task ARetryAfterAFailureSendsWhatTheCoordinatorEditedLast()
    {
        await ApprovedJobAsync(fromEmail: true);
        sender.Failure = new EmailDeliveryException("The mail server did not answer.");
        await Assert.ThrowsAsync<EmailDeliveryException>(() => Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest { SentTo = "wrong@alnoor.com", Message = "First text" })));

        sender.Failure = null;
        await Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator",
            new DeliverCertificatesRequest { SentTo = "right@alnoor.com", Cc = ["copy@alnoor.com"], Message = "Second text" }));

        var email = Assert.Single(sender.Sent);
        Assert.Equal("right@alnoor.com", email.ToAddress);
        await using var db = CreateContext();
        var row = Assert.Single(db.CertificateDeliveries.ToList());
        Assert.Equal(("right@alnoor.com", "copy@alnoor.com", "Second text", true), (row.ToAddress, row.CcAddresses, row.BodyText, row.Sent));
    }

    [Fact]
    public async Task TheClientArchiveAndHistoryShowWhenAndToWhomTheCertificatesWereSent()
    {
        await ApprovedJobAsync(fromEmail: true);
        // The job's own answer carries the request, so the coordinator's screen needs one call; the inspector gets none.
        var machines = await Run(true, service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"));
        Assert.Equal("Al Noor Industries", machines.Request!.ClientName);
        Assert.Equal(2, machines.Request.Items.Count);
        Assert.Null((await Run(true, service => service.ListMachinesAsync(requestId, inspector.Id, "inspector"))).Request);

        // Filed at approval, before anything is sent: no delivery yet.
        var before = await ArchiveAsync();
        Assert.Equal(2, before.Count);
        Assert.All(before, row => Assert.Null(row.Delivery));

        await Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator",
            new DeliverCertificatesRequest { SentTo = "quality@alnoor.com", Cc = ["boss@alnoor.com"], Subject = "Our certificates" }));

        var after = await ArchiveAsync();
        Assert.All(after, row =>
        {
            Assert.True(row.Delivery!.EmailSent);
            Assert.Equal("quality@alnoor.com", row.Delivery.SentTo);
            Assert.Equal(["boss@alnoor.com"], row.Delivery.Cc);
            Assert.Equal("Our certificates", row.Delivery.Subject);
        });

        // The client's history carries the same issued certificates under their request.
        await using var db = CreateContext();
        var history = (await new ClientService(new HistoryClients(db), new EfUserRepository(db), new EfCertificateFileRepository(db)).GetHistoryAsync(clientId))!;
        var issued = Assert.Single(history.Requests, value => value.Request.Id == requestId).IssuedCertificates!;
        Assert.Equal(2, issued.Count);
        Assert.All(issued, row => Assert.Equal("quality@alnoor.com", row.Delivery!.SentTo));
        Assert.All(issued, row => Assert.StartsWith("CERT-", row.CertificateNumber));
    }

    [Fact]
    public async Task ACertificateHandedOverWithoutAnEmailShowsThatInTheArchive()
    {
        await ApprovedJobAsync(fromEmail: false);
        await Run(true, service => service.DeliverManuallyAsync(requestId, coordinator.Id, "coordinator",
            new ManualDeliveryRequest { DeliveredTo = "Mr Hassan at reception" }));

        Assert.All(await ArchiveAsync(), row =>
        {
            Assert.False(row.Delivery!.EmailSent);
            Assert.Equal("Mr Hassan at reception", row.Delivery.SentTo);
            Assert.Empty(row.Delivery.Cc);
        });
    }

    /// <summary>The client's history without the newest-first ordering of the real query, which SQLite cannot do on dates.</summary>
    private sealed class HistoryClients(InspectaDbContext db) : IClientRepository
    {
        private readonly EfClientRepository inner = new(db);
        public Task AddAsync(Client client, CancellationToken cancellationToken = default) => inner.AddAsync(client, cancellationToken);
        public Task<Client?> GetByIdAsync(Guid clientId, bool tracking, CancellationToken cancellationToken = default) => inner.GetByIdAsync(clientId, tracking, cancellationToken);
        public Task<IReadOnlyList<Client>> ListAsync(bool includeArchived, CancellationToken cancellationToken = default) => inner.ListAsync(includeArchived, cancellationToken);
        public async Task<IReadOnlyList<InspectionRequest>> GetRequestHistoryAsync(Guid clientId, CancellationToken cancellationToken = default) =>
            await db.Requests.Include(value => value.Items).Include(value => value.Certificates).Where(value => value.ClientId == clientId).ToListAsync(cancellationToken);
        public Task<(int Requests, int Items, int Reminders)> CountDependenciesAsync(Guid clientId, CancellationToken cancellationToken = default) => inner.CountDependenciesAsync(clientId, cancellationToken);
        public Task RemoveAsync(Client client, CancellationToken cancellationToken = default) => inner.RemoveAsync(client, cancellationToken);
        public Task SaveAsync(CancellationToken cancellationToken = default) => inner.SaveAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ClientCertificateResponse>> ArchiveAsync()
    {
        await using var db = CreateContext();
        return await new ClientCertificateService(new EfClientRepository(db), new EfCertificateFileRepository(db)).ListAsync(clientId);
    }

    [Fact]
    public async Task ATestCopyGoesOnlyToTheSignedInUserAndChangesNothing()
    {
        await ApprovedJobAsync(fromEmail: true);

        var test = await Run(true, service => service.SendTestDeliveryAsync(requestId, coordinator.Id, "coordinator",
            new DeliverCertificatesRequest { SentTo = "client@alnoor.com", Cc = ["boss@alnoor.com"], Message = "Draft text" }));

        Assert.Equal("coordinator@example.com", test.SentTo);
        Assert.Equal("[TEST] Inspection Certificates - Al Noor Industries", test.Subject);
        var email = Assert.Single(sender.Sent);
        Assert.Equal("coordinator@example.com", email.ToAddress);
        Assert.True(email.Cc is null or { Count: 0 });
        Assert.Equal("[TEST] Inspection Certificates - Al Noor Industries", email.Subject);
        Assert.Equal("Draft text", email.BodyText);
        Assert.Null(email.InReplyTo);
        Assert.Equal(2, 1 + email.MoreAttachments!.Count);

        // Nothing was recorded and the job is untouched: the real send is still possible.
        await using (var db = CreateContext()) Assert.Empty(db.CertificateDeliveries.ToList());
        Assert.Equal(JobStatus.CertificateApproved, (await Run(true, service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"))).RequestStatus);
        Assert.True((await Run(true, service => service.PreviewDeliveryAsync(requestId, coordinator.Id, "coordinator"))).CanSend);

        // Off while email is off, and not for inspectors.
        Assert.Equal("email_disabled", (await Assert.ThrowsAsync<AuthException>(() => Run(false, service => service.SendTestDeliveryAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest())))).Code);
        await Assert.ThrowsAsync<AuthException>(() => Run(true, service => service.SendTestDeliveryAsync(
            requestId, inspector.Id, "inspector", new DeliverCertificatesRequest())));
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task WithoutAnAddressSendingIsRefusedAndTheJobCanBeMarkedDeliveredWithoutAnEmail()
    {
        await ApprovedJobAsync(fromEmail: false);
        await using (var db = CreateContext())
            await db.Database.ExecuteSqlRawAsync("UPDATE Clients SET CertificatesEmail = NULL");

        var preview = await Run(true, service => service.PreviewDeliveryAsync(requestId, coordinator.Id, "coordinator"));
        Assert.False(preview.CanSend);
        Assert.Null(preview.To);
        Assert.Contains("no address to email", preview.Blockers!.Single());
        var refused = await Assert.ThrowsAsync<AuthException>(() => Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest())));
        Assert.Equal("no_recipient", refused.Code);
        Assert.Equal(JobStatus.CertificateApproved, (await Run(true, service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"))).RequestStatus);

        var manual = await Run(true, service => service.DeliverManuallyAsync(requestId, coordinator.Id, "coordinator",
            new ManualDeliveryRequest { DeliveredTo = "Mr Hassan at reception", Note = "Printed and handed over" }));

        Assert.Equal(JobStatus.SentToClient, manual.RequestStatus);
        Assert.False(manual.Delivery!.EmailSent);
        Assert.Equal("Mr Hassan at reception", manual.Delivery.SentTo);
        Assert.All(manual.Machines, machine => Assert.Equal(MachineStatus.Delivered, machine.Status));
        Assert.Empty(sender.Sent);
        await using var check = CreateContext();
        var history = check.Requests.Include(value => value.History).Single(value => value.Id == requestId).History.OrderBy(value => value.OccurredAt.UtcDateTime).Last();
        Assert.Equal(JobStatus.SentToClient, history.ToStatus);
        Assert.Equal("Handed over without an email to Mr Hassan at reception: Printed and handed over", history.Note);

        // The job still says so afterwards, and it cannot be delivered twice.
        var later = await Run(true, service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"));
        Assert.False(later.Delivery!.EmailSent);
        Assert.Equal("Mr Hassan at reception", later.Delivery.SentTo);
        Assert.Equal("not_approved", (await Assert.ThrowsAsync<AuthException>(() => Run(true, service => service.DeliverManuallyAsync(
            requestId, coordinator.Id, "coordinator", new ManualDeliveryRequest())))).Code);
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthException>(() => Run(true, service => service.DeliverManuallyAsync(
            requestId, inspector.Id, "inspector", new ManualDeliveryRequest())))).StatusCode);
    }

    [Fact]
    public async Task ARequestWithoutAnEmailUsesTheClientsCertificatesAddress()
    {
        await ApprovedJobAsync(fromEmail: false);

        var preview = await Run(true, service => service.PreviewDeliveryAsync(requestId, coordinator.Id, "coordinator"));
        Assert.Equal("certificates@alnoor.com", preview.To);
        Assert.False(preview.RepliesToOriginalEmail);
        Assert.StartsWith("Inspection Certificates - Al Noor Industries", preview.Subject);

        await Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));
        Assert.Equal("certificates@alnoor.com", Assert.Single(sender.Sent).ToAddress);
    }

    [Fact]
    public async Task WithEmailOnButNoMailboxTheDeliveryIsRefusedAndNothingChanges()
    {
        await ApprovedJobAsync(fromEmail: true, configureMailbox: false);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest())));

        Assert.Equal(JobStatus.CertificateApproved, (await Run(true, service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"))).RequestStatus);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task AllTheCertificatesOfTheJobComeInOneZipForTheCoordinatorSideOnly()
    {
        await ApprovedJobAsync(fromEmail: true);

        var zip = await Run(false, service => service.GetCertificatesZipAsync(requestId, coordinator.Id, "coordinator"));

        Assert.Equal("application/zip", zip.ContentType);
        Assert.StartsWith("certificates-Al-Noor-Industries", zip.FileName);
        using var archive = new ZipArchive(new MemoryStream(zip.Content));
        Assert.Equal(2, archive.Entries.Count);
        Assert.All(archive.Entries, entry => Assert.EndsWith(".pdf", entry.FullName));
        var denied = await Assert.ThrowsAsync<AuthException>(() => Run(false, service => service.GetCertificatesZipAsync(requestId, inspector.Id, "inspector")));
        Assert.Equal(403, denied.StatusCode);
        await Assert.ThrowsAsync<AuthException>(() => Run(false, service => service.PreviewDeliveryAsync(requestId, inspector.Id, "inspector")));
    }

    [Fact]
    public async Task NothingIsAvailableForDeliveryBeforeEveryMachineIsApproved()
    {
        await StartJobAsync(fromEmail: true, configureMailbox: true);

        var early = await Assert.ThrowsAsync<AuthException>(() => Run(true, service => service.PreviewDeliveryAsync(requestId, coordinator.Id, "coordinator")));
        Assert.Equal("not_approved", early.Code);
        await Assert.ThrowsAsync<AuthException>(() => Run(true, service => service.GetCertificatesZipAsync(requestId, coordinator.Id, "coordinator")));
    }

    // ---- The 30-day clock of the inspector's report files ---------------------------------------------------------

    // Two small reports, weld.pdf first (it is the older one), so the order of the email is certain.
    private async Task<Guid[]> SeedReportsAsync() =>
        [await SeedReportAsync("weld.pdf", 20, minutesAgo: 10), await SeedReportAsync("photos.pdf", 20, minutesAgo: 5)];

    private async Task<Guid> SeedReportAsync(string name, int size, Guid? itemId = null, int minutesAgo = 0, Guid? forRequest = null)
    {
        var content = new byte[size];
        "%PDF-1.4 x"u8.CopyTo(content);
        await using var db = CreateContext();
        var file = JobReportFile.Create(
            manager.Id, forRequest ?? requestId, itemId, inspector.Id, inspector.FullName, name, "application/pdf", content, DateTimeOffset.UtcNow.AddMinutes(-minutesAgo));
        db.JobReportFiles.Add(file);
        await db.SaveChangesAsync();
        return file.Id;
    }

    private async Task<DateTimeOffset?[]> ExpiriesAsync()
    {
        await using var db = CreateContext();
        return db.JobReportFiles.Where(file => file.RequestId == requestId).Select(file => file.ExpiresAt).AsEnumerable().ToArray();
    }

    private static void AssertThirtyDaysFromNow(DateTimeOffset?[] expiries)
    {
        Assert.Equal(2, expiries.Length);
        Assert.All(expiries, expires =>
        {
            Assert.NotNull(expires);
            Assert.InRange(expires!.Value, DateTimeOffset.UtcNow.AddDays(30).AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(30).AddMinutes(5));
        });
    }

    [Fact]
    public async Task DeliveryByEmailStampsTheExpiryOnEveryReport()
    {
        await ApprovedJobAsync(fromEmail: true);
        await SeedReportsAsync();
        Assert.All(await ExpiriesAsync(), expires => Assert.Null(expires));

        await Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));

        Assert.Single(sender.Sent);
        AssertThirtyDaysFromNow(await ExpiriesAsync());
    }

    [Fact]
    public async Task DeliveryWithEmailSwitchedOffStampsTheExpiryOnEveryReport()
    {
        await ApprovedJobAsync(fromEmail: true);
        await SeedReportsAsync();

        await Run(false, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));

        Assert.Empty(sender.Sent);
        AssertThirtyDaysFromNow(await ExpiriesAsync());
    }

    [Fact]
    public async Task ManualDeliveryStartsTheThirtyDaysToo()
    {
        await ApprovedJobAsync(fromEmail: true);
        await SeedReportsAsync();

        await Run(true, service => service.DeliverManuallyAsync(requestId, coordinator.Id, "coordinator", new ManualDeliveryRequest { DeliveredTo = "Mr Hassan" }));

        Assert.Empty(sender.Sent);
        AssertThirtyDaysFromNow(await ExpiriesAsync());
    }

    [Fact]
    public async Task AFailedEmailLeavesTheReportsWithoutAnExpiry()
    {
        microsoftMailbox = true;
        var stub = new StubMailboxAuthenticator { Reject = true };
        authenticator = stub;
        await ApprovedJobAsync(fromEmail: true);
        await SeedReportsAsync();

        await Assert.ThrowsAsync<AuthException>(() =>
            Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest())));
        Assert.All(await ExpiriesAsync(), expires => Assert.Null(expires));

        stub.Reject = false;
        await Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));
        AssertThirtyDaysFromNow(await ExpiriesAsync());
    }

    [Fact]
    public async Task ADeliveryOfAJobWithoutReportsSendsWhatItSentBefore()
    {
        await ApprovedJobAsync(fromEmail: true);

        var delivered = await Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));

        Assert.True(delivered.Delivery!.EmailSent);
        var email = Assert.Single(sender.Sent);
        Assert.Equal(2, 1 + email.MoreAttachments!.Count); // the two certificates, nothing else
        Assert.EndsWith(".pdf", email.AttachmentFileName);
        await using var db = CreateContext();
        Assert.Empty(db.JobReportFiles.ToList());
        var names = db.CertificateDeliveries.Single().AttachmentNames.Split("; ");
        Assert.Equal(2, names.Length);
        Assert.All(names, name => Assert.EndsWith(".pdf", name));
    }

    // ---- Reports go with the certificates (step 2) ---------------------------------------------------------------

    private static string[] SentNames(OutgoingEmail email) => [email.AttachmentFileName!, .. email.MoreAttachments!.Select(file => file.FileName)];

    private static byte[] Pdf(int size)
    {
        var content = new byte[size];
        "%PDF-1.4 x"u8.CopyTo(content);
        return content;
    }

    // Content that does not shrink in a ZIP, like real PDFs and photos: a file of zeros would compress to nothing and prove nothing about sizes.
    private static byte[] IncompressiblePdf(int size)
    {
        var content = new byte[size];
        new Random(42).NextBytes(content);
        "%PDF-1.4 x"u8.CopyTo(content);
        return content;
    }

    /// <summary>The issued certificates are drawn again as bigger files, so a job can carry the 18 MB ZIP rule without a real 10 MB PDF.</summary>
    private async Task MakeCertificatesBigAsync(int bytesEach)
    {
        await using var db = CreateContext();
        foreach (var file in db.IssuedCertificateFiles.Include(value => value.Body).ToList())
            file.Reissue(file.CertificateNumber, file.IssuedAt, file.FileName, IncompressiblePdf(bytesEach));
        await db.SaveChangesAsync();
    }

    private async Task<JobStatus> StatusAsync()
    {
        await using var db = CreateContext();
        return db.Requests.Single(value => value.Id == requestId).Status;
    }

    [Fact]
    public async Task ReportsGoWithTheCertificatesByDefaultAfterThemAndBeforeTheCoordinatorsOwnFiles()
    {
        await ApprovedJobAsync(fromEmail: true);
        await SeedReportsAsync();
        var notes = new Application.Contracts.Mail.EmailAttachmentUpload("notes.txt", Convert.ToBase64String("Please keep these."u8.ToArray()));

        var delivered = await Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest { Attachments = [notes] }));

        Assert.True(delivered.Delivery!.EmailSent);
        var email = Assert.Single(sender.Sent);
        var names = SentNames(email);
        Assert.Equal(5, names.Length);
        Assert.EndsWith(".pdf", names[0]);
        Assert.EndsWith(".pdf", names[1]);
        Assert.Equal(["weld.pdf", "photos.pdf", "notes.txt"], names[2..]);
        Assert.Equal("application/pdf", email.MoreAttachments![1].ContentType);
        Assert.Equal(Pdf(20), email.MoreAttachments[1].Content);
        await using var db = CreateContext();
        Assert.Equal(string.Join("; ", names), db.CertificateDeliveries.Single().AttachmentNames);
        Assert.Equal(JobStatus.SentToClient, await StatusAsync());
        AssertThirtyDaysFromNow(await ExpiriesAsync()); // the clock still starts for every report, ticked or not
    }

    [Fact]
    public async Task UntickedReportsAreNotSent()
    {
        await ApprovedJobAsync(fromEmail: true);
        var ids = await SeedReportsAsync();

        await Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest { ExcludedReportIds = [ids[0]] }));

        var names = SentNames(Assert.Single(sender.Sent));
        Assert.Equal(3, names.Length);
        Assert.Equal("photos.pdf", names[2]);
        Assert.DoesNotContain("weld.pdf", names);
        AssertThirtyDaysFromNow(await ExpiriesAsync()); // the unticked one is still a report of the job and expires with the others
    }

    [Fact]
    public async Task WithEveryReportUntickedTheEmailIsExactlyWhatAJobWithoutReportsSends()
    {
        await ApprovedJobAsync(fromEmail: true);
        var ids = await SeedReportsAsync();

        await Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest { ExcludedReportIds = ids }));

        var names = SentNames(Assert.Single(sender.Sent));
        Assert.Equal(2, names.Length);
        Assert.All(names, name => Assert.EndsWith(".pdf", name));
        Assert.DoesNotContain("weld.pdf", names);
        await using var db = CreateContext();
        Assert.Equal(2, db.CertificateDeliveries.Single().AttachmentNames.Split("; ").Length);
    }

    [Fact]
    public async Task AnExcludedIdThatIsNotAReportOfThisJobIsIgnoredAndAnotherJobsReportIsNeverSent()
    {
        await ApprovedJobAsync(fromEmail: true);
        await SeedReportsAsync();
        Guid otherJobsReport;
        await using (var db = CreateContext())
        {
            var other = InspectionRequest.Create("Other Ltd", "Giza", null, [RequestItem.Create("Mobile crane", 1)], coordinator.Id, managerId: manager.Id);
            db.Requests.Add(other);
            await db.SaveChangesAsync();
            otherJobsReport = await SeedReportAsync("other.pdf", 20, forRequest: other.Id);
        }

        // An id that is not (or no longer) a report of this job matches nothing, so it is ignored: it cannot hide a report and a retry never fails on it.
        foreach (var wrong in new[] { otherJobsReport, Guid.NewGuid() })
            await Run(true, service => service.SendTestDeliveryAsync(
                requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest { ExcludedReportIds = [wrong] }));
        Assert.All(sender.Sent, email => Assert.Equal(["weld.pdf", "photos.pdf"], SentNames(email)[2..]));
        Assert.Equal(2, sender.Sent.Count);

        sender.Sent.Clear();
        await Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest { ExcludedReportIds = [otherJobsReport, Guid.NewGuid()] }));

        var names = SentNames(Assert.Single(sender.Sent));
        Assert.Equal(["weld.pdf", "photos.pdf"], names[2..]); // both own reports went, the other job's file never
        Assert.DoesNotContain("other.pdf", names);
        await using var check = CreateContext();
        Assert.DoesNotContain("other.pdf", check.CertificateDeliveries.Single().AttachmentNames);
        Assert.Null(check.JobReportFiles.Single(file => file.Id == otherJobsReport).ExpiresAt); // the other job is untouched
    }

    [Fact]
    public async Task ARetryAfterTheEmailWentOutButWasNotSavedSaysAlreadyEmailedEvenWhenAnUntickedReportIsGone()
    {
        await ApprovedJobAsync(fromEmail: true);
        await SeedReportsAsync();
        await using (var db = CreateContext())
        {
            // The first call sent the email and then failed before it saved the job: the record says sent, the job is still approved.
            var earlier = CertificateDelivery.Create(manager.Id, requestId, "buyer@alnoor.com", "Inspection Certificates", "body", "<first@alnoor.com>", ["a.pdf"]);
            earlier.MarkSent();
            db.CertificateDeliveries.Add(earlier);
            await db.SaveChangesAsync();
        }

        var again = await Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest { ExcludedReportIds = [Guid.NewGuid()] }));

        Assert.True(again.Delivery!.EmailSent);
        Assert.Contains("already emailed", again.Delivery.Notice);
        Assert.Empty(sender.Sent); // never a second email
        Assert.Equal(0, reportLoads); // and the report bytes were not read for it
        Assert.Equal(JobStatus.SentToClient, await StatusAsync());
        AssertThirtyDaysFromNow(await ExpiriesAsync());
    }

    [Fact]
    public async Task TheReportBytesAreNotReadWhenTheEmailIsTooLargeAndAreReadOnceOtherwise()
    {
        await ApprovedJobAsync(fromEmail: true);
        var ids = new[]
        {
            await SeedReportAsync("scan1.pdf", 10 * 1024 * 1024, minutesAgo: 10),
            await SeedReportAsync("scan2.pdf", 10 * 1024 * 1024, minutesAgo: 5)
        };

        foreach (var attempt in new Func<MachineInspectionService, Task>[]
                 {
                     service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()),
                     service => service.SendTestDeliveryAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest())
                 })
        {
            var refused = await Assert.ThrowsAsync<AuthException>(() => Run(true, async service => { await attempt(service); return 0; }));
            Assert.Equal("email_too_large", refused.Code);
        }

        Assert.Equal(0, reportLoads); // the size is known from the list, before any bytes are read

        await Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest { ExcludedReportIds = [ids[0]] }));
        Assert.Equal(1, reportLoads);
    }

    [Fact]
    public async Task ADoubleClickOrATestEmailDuringARealSendDoesNotReadTheReportBytesAgain()
    {
        await ApprovedJobAsync(fromEmail: true);
        await SeedReportsAsync();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sender.Hold = release.Task;

        var first = Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));
        await sender.Started.Task;

        var second = await Assert.ThrowsAsync<AuthException>(() =>
            Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest())));
        var test = await Assert.ThrowsAsync<AuthException>(() =>
            Run(true, service => service.SendTestDeliveryAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest())));
        Assert.Equal("delivery_in_progress", second.Code);
        Assert.Equal("delivery_in_progress", test.Code);

        release.SetResult();
        await first;
        Assert.Single(sender.Sent);
        Assert.Equal(1, reportLoads);
    }

    [Fact]
    public void TheNamesOfTheAttachmentsAreCutToTheColumnSoAHugeListCannotFailTheSave()
    {
        var names = Enumerable.Range(0, 100).Select(number => $"CERT-2026-{number:0000}-{new string('c', 60)}.pdf")
            .Concat(Enumerable.Range(0, 20).Select(number => new string((char)('a' + number), 145) + ".pdf"));
        Assert.True(string.Join("; ", names).Length > CertificateDelivery.MaxAttachmentNamesLength); // the list really is too long for the column

        var created = CertificateDelivery.Create(manager.Id, Guid.NewGuid(), "buyer@alnoor.com", "s", "b", "<id@alnoor.com>", names);
        Assert.Equal(CertificateDelivery.MaxAttachmentNamesLength, created.AttachmentNames.Length);
        Assert.StartsWith("CERT-2026-0000", created.AttachmentNames);

        created.Revise("buyer@alnoor.com", [], "s", "b", names.Concat(names));
        Assert.Equal(CertificateDelivery.MaxAttachmentNamesLength, created.AttachmentNames.Length);

        var short_ = CertificateDelivery.Create(manager.Id, Guid.NewGuid(), "buyer@alnoor.com", "s", "b", "<id@alnoor.com>", ["a.pdf", "b.pdf"]);
        Assert.Equal("a.pdf; b.pdf", short_.AttachmentNames); // a normal list is kept as it is
    }

    [Fact]
    public async Task TooLargeEmailWithReportsIsRefusedAndNothingChanges()
    {
        await ApprovedJobAsync(fromEmail: true);
        var ids = new[]
        {
            await SeedReportAsync("scan1.pdf", 10 * 1024 * 1024, minutesAgo: 10),
            await SeedReportAsync("scan2.pdf", 10 * 1024 * 1024, minutesAgo: 5)
        };

        var refused = await Assert.ThrowsAsync<AuthException>(() => Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest())));

        Assert.Equal("email_too_large", refused.Code);
        Assert.Equal(409, refused.StatusCode);
        Assert.Contains("17", refused.Message);
        Assert.Contains("MB", refused.Message);
        Assert.Empty(sender.Sent);
        Assert.Equal(JobStatus.CertificateApproved, await StatusAsync());
        await using (var db = CreateContext()) Assert.Empty(db.CertificateDeliveries.ToList());
        Assert.All(await ExpiriesAsync(), expires => Assert.Null(expires));

        // Unticking one report brings it under the cap and the same call works.
        await Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest { ExcludedReportIds = [ids[0]] }));
        Assert.Equal("scan2.pdf", SentNames(Assert.Single(sender.Sent))[^1]);
    }

    [Fact]
    public async Task TheCapCountsTheCertificatesTheExtraFilesAndTheReportsTogether()
    {
        await ApprovedJobAsync(fromEmail: true);
        await SeedReportAsync("scan.pdf", 6 * 1024 * 1024);
        var big = new Application.Contracts.Mail.EmailAttachmentUpload("extra.pdf", Convert.ToBase64String(IncompressiblePdf(15 * 1024 * 1024)));

        var refused = await Assert.ThrowsAsync<AuthException>(() => Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest { Attachments = [big] })));

        Assert.Equal("email_too_large", refused.Code);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task TheCapFollowsTheConfiguredValue()
    {
        reportOptions = new JobReportOptions(maxEmailBytes: 25L * 1024 * 1024);
        await ApprovedJobAsync(fromEmail: true);
        await SeedReportAsync("scan1.pdf", 10 * 1024 * 1024);
        await SeedReportAsync("scan2.pdf", 10 * 1024 * 1024);

        await Run(true, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));

        Assert.Equal(4, SentNames(Assert.Single(sender.Sent)).Length);
        Assert.Equal(1024 * 1024, new JobReportOptions(maxEmailBytes: 0).MaxEmailBytes);
        Assert.Equal(25L * 1024 * 1024, new JobReportOptions(maxEmailBytes: long.MaxValue).MaxEmailBytes);
        Assert.Equal(17825792, new JobReportOptions().MaxEmailBytes); // 17 MB raw: about 23 MB once the mail is base64 encoded
    }

    [Fact]
    public async Task ADeliveryWithoutReportsIsNotCheckedAgainstTheCapAndStillSendsTheBigZipWithTheBigExtraFile()
    {
        await ApprovedJobAsync(fromEmail: true);
        await MakeCertificatesBigAsync(10 * 1024 * 1024); // 20 MB of certificates: above 18 MB, so they travel as one ZIP
        var extra = new Application.Contracts.Mail.EmailAttachmentUpload("extra.pdf", Convert.ToBase64String(IncompressiblePdf(15 * 1024 * 1024)));

        var delivered = await Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest { Attachments = [extra] }));

        Assert.True(delivered.Delivery!.EmailSent);
        Assert.Equal(["certificates.zip", "extra.pdf"], SentNames(Assert.Single(sender.Sent)));
        await using var db = CreateContext();
        Assert.Equal("certificates.zip; extra.pdf", db.CertificateDeliveries.Single().AttachmentNames);
        Assert.Equal(JobStatus.SentToClient, await StatusAsync());
    }

    [Fact]
    public async Task TheBigZipAndExtraFileStillSendWhenEveryReportIsUnticked()
    {
        await ApprovedJobAsync(fromEmail: true);
        await MakeCertificatesBigAsync(10 * 1024 * 1024);
        var ids = await SeedReportsAsync();
        var extra = new Application.Contracts.Mail.EmailAttachmentUpload("extra.pdf", Convert.ToBase64String(IncompressiblePdf(15 * 1024 * 1024)));

        // With the reports the same email is over the cap...
        var refused = await Assert.ThrowsAsync<AuthException>(() => Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest { Attachments = [extra] })));
        Assert.Equal("email_too_large", refused.Code);

        // ...and without them it is exactly what it always was.
        await Run(true, service => service.DeliverAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest { Attachments = [extra], ExcludedReportIds = ids }));
        Assert.Equal(["certificates.zip", "extra.pdf"], SentNames(Assert.Single(sender.Sent)));
    }

    [Fact]
    public async Task TheTestEmailCarriesTheSameReportsAndChangesNothing()
    {
        await ApprovedJobAsync(fromEmail: true);
        var ids = await SeedReportsAsync();

        var test = await Run(true, service => service.SendTestDeliveryAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest { ExcludedReportIds = [ids[0]] }));

        Assert.StartsWith("[TEST]", test.Subject);
        var email = Assert.Single(sender.Sent);
        Assert.Equal("coordinator@example.com", email.ToAddress);
        Assert.Equal(3, SentNames(email).Length);
        Assert.Equal("photos.pdf", SentNames(email)[2]);
        Assert.Equal(JobStatus.CertificateApproved, await StatusAsync());
        await using (var db = CreateContext()) Assert.Empty(db.CertificateDeliveries.ToList());
        Assert.All(await ExpiriesAsync(), expires => Assert.Null(expires));

        // Without a choice every report goes in the test too.
        sender.Sent.Clear();
        await Run(true, service => service.SendTestDeliveryAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));
        Assert.Equal(4, SentNames(Assert.Single(sender.Sent)).Length);
    }

    [Fact]
    public async Task TheTestEmailIsRefusedWhenItIsTooLargeToo()
    {
        await ApprovedJobAsync(fromEmail: true);
        await SeedReportAsync("scan1.pdf", 10 * 1024 * 1024);
        await SeedReportAsync("scan2.pdf", 10 * 1024 * 1024);

        var refused = await Assert.ThrowsAsync<AuthException>(() => Run(true, service => service.SendTestDeliveryAsync(
            requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest())));

        Assert.Equal("email_too_large", refused.Code);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task ThePreviewListsTheReportsAndTheirNamesAppearInTheAttachments()
    {
        await ApprovedJobAsync(fromEmail: true);
        var withoutReports = await Run(true, service => service.PreviewDeliveryAsync(requestId, coordinator.Id, "coordinator"));
        Assert.Empty(withoutReports.Reports!);
        Assert.Equal(0, withoutReports.ReportsTotalBytes);
        Assert.Equal(17825792, withoutReports.MaxEmailBytes);
        Assert.Equal(2, withoutReports.Attachments.Count);
        Assert.True(withoutReports.CanSend);

        var ids = new[] { await SeedReportAsync("weld.pdf", 100, itemIds[0], minutesAgo: 10), await SeedReportAsync("photos.pdf", 50, minutesAgo: 5) };

        var preview = await Run(true, service => service.PreviewDeliveryAsync(requestId, coordinator.Id, "coordinator"));

        Assert.Equal(ids, preview.Reports!.Select(report => report.Id));
        Assert.Equal(["weld.pdf", "photos.pdf"], preview.Reports!.Select(report => report.FileName));
        Assert.Equal([100L, 50L], preview.Reports!.Select(report => report.SizeBytes));
        Assert.Equal("Forklift Truck", preview.Reports![0].MachineName);
        Assert.Null(preview.Reports[1].MachineName);
        Assert.Equal(150, preview.ReportsTotalBytes);
        Assert.Equal(4, preview.Attachments.Count);
        Assert.Equal(["weld.pdf", "photos.pdf"], preview.Attachments.Skip(2));
        Assert.True(preview.CanSend);
        Assert.Empty(preview.Blockers!);
    }

    [Fact]
    public async Task ThePreviewWarnsWhenTheReportsAreTooLargeToEmailTogether()
    {
        await ApprovedJobAsync(fromEmail: true);
        await SeedReportAsync("scan1.pdf", 10 * 1024 * 1024, minutesAgo: 10);
        await SeedReportAsync("scan2.pdf", 10 * 1024 * 1024, minutesAgo: 5);

        var preview = await Run(true, service => service.PreviewDeliveryAsync(requestId, coordinator.Id, "coordinator"));

        Assert.Equal(20L * 1024 * 1024, preview.ReportsTotalBytes);
        Assert.Contains(preview.Blockers!, blocker => blocker.Contains("Untick some reports"));
        Assert.False(preview.CanSend);
        Assert.Equal(2, preview.Reports!.Count); // still listed, so they can be unticked
    }

    [Fact]
    public async Task TheCertificatesZipHasTheReportsUntilTheyExpireThenItIsExactlyTodaysZip()
    {
        await ApprovedJobAsync(fromEmail: true);
        var before = await Run(false, service => service.GetCertificatesZipAsync(requestId, coordinator.Id, "coordinator"));
        await SeedReportsAsync();
        await SeedReportAsync("weld.pdf", 30, minutesAgo: 1); // a second file with the same name

        var withReports = await Run(false, service => service.GetCertificatesZipAsync(requestId, coordinator.Id, "coordinator"));

        var names = ZipNames(withReports.Content);
        Assert.Equal(2, names.Count(name => name.EndsWith(".pdf") && !name.StartsWith("reports/")));
        Assert.Equal(["reports/weld.pdf", "reports/photos.pdf", "reports/weld (2).pdf"], names.Where(name => name.StartsWith("reports/")));
        Assert.Equal(ZipNames(before.Content).Order(), names.Where(name => !name.StartsWith("reports/")).Order());

        // Delivered, then the 30 days run out: every report is gone from the ZIP and it is today's ZIP again.
        await Run(false, service => service.DeliverManuallyAsync(requestId, coordinator.Id, "coordinator", new ManualDeliveryRequest()));
        var delivered = await Run(false, service => service.GetCertificatesZipAsync(requestId, coordinator.Id, "coordinator"));
        Assert.Equal(5, ZipNames(delivered.Content).Count);

        await using (var db = CreateContext())
        {
            foreach (var file in db.JobReportFiles.Where(value => value.RequestId == requestId).ToList())
                typeof(JobReportFile).GetProperty(nameof(JobReportFile.ExpiresAt))!.SetValue(file, DateTimeOffset.UtcNow.AddMinutes(-1));
            await db.SaveChangesAsync();
        }

        var expired = await Run(false, service => service.GetCertificatesZipAsync(requestId, coordinator.Id, "coordinator"));
        Assert.Equal(ZipNames(before.Content).Order(), ZipNames(expired.Content).Order());
    }

    private static IReadOnlyList<string> ZipNames(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip));
        return archive.Entries.Select(entry => entry.FullName).ToArray();
    }

    [Fact]
    public void TheDeliveryRecordHasRoomForTheNamesOfEveryCertificateReportAndExtraFile()
    {
        using var db = CreateContext();
        var names = db.Model.FindEntityType(typeof(CertificateDelivery))!.FindProperty(nameof(CertificateDelivery.AttachmentNames))!;

        Assert.Equal(8000, names.GetMaxLength());
        // 30 certificates, 20 reports with the longest allowed names and 5 extra files, joined the way the record keeps them.
        var longest = string.Join("; ", Enumerable.Repeat(new string('a', 60) + ".pdf", 30).Concat(Enumerable.Repeat(new string('r', 145) + ".pdf", 20)).Concat(Enumerable.Repeat(new string('x', 145) + ".pdf", 5)));
        Assert.True(longest.Length <= 8000, $"{longest.Length} characters");
    }

    [Fact]
    public async Task TheRetentionFollowsTheConfiguredNumberOfDays()
    {
        reportOptions = new JobReportOptions(7);
        await ApprovedJobAsync(fromEmail: true);
        await SeedReportsAsync();

        await Run(false, service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));

        Assert.Empty(sender.Sent);
        Assert.All(await ExpiriesAsync(), expires => Assert.InRange(
            expires!.Value, DateTimeOffset.UtcNow.AddDays(7).AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(7).AddMinutes(5)));
    }

    // ---- Setup --------------------------------------------------------------------------------------------------

    private async Task StartJobAsync(bool fromEmail, bool configureMailbox)
    {
        await using (var db = CreateContext())
        {
            clientId = (await new ClientService(new EfClientRepository(db), new EfUserRepository(db)).CreateAsync(new CreateClientRequest
            {
                Name = "Al Noor Industries", Location = "Cairo", CertificatesEmail = "certificates@alnoor.com"
            }, manager.Id)).Id;
        }

        await using (var db = CreateContext())
        {
            if (configureMailbox)
                db.CoordinatorEmailConfigs.Add(microsoftMailbox
                    ? CoordinatorEmailConfig.CreateMicrosoft(coordinator.Id, manager.Id, "coordinator@example.com", "enc-refresh", "tenant-1")
                    : CoordinatorEmailConfig.Create(
                        coordinator.Id, manager.Id, "imap.example.com", 993, "coordinator@example.com", "secret", true));
            var items = new[] { RequestItem.Create("Forklift Truck", 1, capacity: "3 ton"), RequestItem.Create("Chain Block", 4) };
            var request = fromEmail
                ? InspectionRequest.CreateFromEmail(manager.Id, coordinator.Id, new IncomingEmail(
                    "42", 1, "Please inspect.", null, "buyer@alnoor.com", "Al Noor", "Inspection request",
                    [], [], [], [], [], "<id@alnoor.com>", null, null, DateTimeOffset.UtcNow.AddMinutes(-5)))
                : InspectionRequest.Create("Al Noor Industries", "Cairo", clientId, items, coordinator.Id, managerId: manager.Id);
            request.UseReviewer(manager.Id);
            if (fromEmail)
            {
                request.UpdateDetails("Al Noor Industries", "Cairo", clientId, null, null);
                foreach (var item in items) request.AddItem(item);
            }

            request.TransitionTo(JobStatus.QuoteDraft, coordinator.Id);
            request.TransitionTo(JobStatus.QuoteSent, coordinator.Id);
            request.TransitionTo(JobStatus.Unassigned, coordinator.Id);
            request.AssignInspector(inspector.Id, DateTimeOffset.UtcNow.AddDays(1), coordinator.Id);
            db.Requests.Add(request);
            await db.SaveChangesAsync();
            requestId = request.Id;
            itemIds = items.Select(item => item.Id).ToArray();
        }
    }

    private async Task ApprovedJobAsync(bool fromEmail, bool configureMailbox = true)
    {
        await StartJobAsync(fromEmail, configureMailbox);
        await Run(false, service => service.StartInspectionAsync(requestId, inspector.Id, "inspector"));
        foreach (var (itemId, key) in itemIds.Zip(new[] { "forklift", "basic" }))
        {
            await Run(false, service => service.SaveFormAsync(requestId, itemId, inspector.Id, "inspector", CompleteForm(key)));
            await Run(false, service => service.SubmitAsync(requestId, itemId, inspector.Id, "inspector"));
        }

        foreach (var itemId in itemIds)
            await Run(false, service => service.ApproveAsync(requestId, itemId, manager.Id, "technical_manager"));
    }

    private static SaveMachineFormRequest CompleteForm(string templateKey)
    {
        var template = CertificateTemplateCatalog.Find(templateKey)!;
        return new SaveMachineFormRequest
        {
            Checklist = template.ChecklistPaths.ToDictionary(path => path, _ => new ChecklistAnswerRequest { Value = "yes" }),
            Answers = new Dictionary<string, bool>
            {
                ["firstExamination"] = false, ["within12Months"] = true, ["examinationScheme"] = true,
                ["exceptionalCircumstances"] = false, ["existingDanger"] = false, ["futureDanger"] = false
            },
            Dates = new Dictionary<string, DateOnly> { ["examination"] = new(2026, 11, 5), ["nextExamination"] = new(2027, 11, 5) },
            Result = InspectionResult.Satisfactory
        };
    }

    private async Task<T> Run<T>(bool emailEnabled, Func<MachineInspectionService, Task<T>> action)
    {
        await using var db = CreateContext();
        var files = new EfCertificateFileRepository(db);
        var delivery = new CertificateDeliveryService(
            new EfRequestRepository(db),
            new EfClientRepository(db),
            new EfCoordinatorEmailConfigRepository(db),
            new EfUserRepository(db),
            new EfCertificateDeliveryRepository(db),
            new QuotationTests.FakeProtector(),
            sender,
            new CertificateDeliveryOptions(emailEnabled),
            mailboxAuthenticator: authenticator,
            reportOptions: reportOptions);
        var service = new MachineInspectionService(
            new EfCertificateRepository(db),
            new EfRequestRepository(db),
            new EfUserRepository(db),
            new EfEquipmentRepository(db),
            new EfClientRepository(db),
            pdfServices.GetRequiredService<IPdfGenerator>(),
            files,
            null,
            delivery,
            jobReports: new CountingReportRepository(new EfJobReportRepository(db), () => Interlocked.Increment(ref reportLoads)),
            reportOptions: reportOptions);
        return await action(service);
    }


    /// <summary>The real repository, counting how often the bytes of chosen reports are read (the overload used for the email).</summary>
    private sealed class CountingReportRepository(IJobReportRepository inner, Action onLoadChosen) : IJobReportRepository
    {
        public Task<IReadOnlyList<JobReportFile>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default) => inner.ListForRequestAsync(requestId, cancellationToken);
        public Task<int> CountForRequestAsync(Guid requestId, DateTimeOffset now, CancellationToken cancellationToken = default) => inner.CountForRequestAsync(requestId, now, cancellationToken);
        public Task<JobReportFile?> GetWithContentAsync(Guid requestId, Guid fileId, CancellationToken cancellationToken = default) => inner.GetWithContentAsync(requestId, fileId, cancellationToken);
        public Task<IReadOnlyList<JobReportFile>> ListWithContentAsync(Guid requestId, CancellationToken cancellationToken = default) => inner.ListWithContentAsync(requestId, cancellationToken);
        public Task<IReadOnlyList<JobReportFile>> ListWithContentAsync(Guid requestId, IReadOnlyCollection<Guid> fileIds, CancellationToken cancellationToken = default)
        {
            onLoadChosen();
            return inner.ListWithContentAsync(requestId, fileIds, cancellationToken);
        }

        public Task<int> DeleteExpiredAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) => inner.DeleteExpiredAsync(now, batchSize, cancellationToken);
        public Task AddAsync(JobReportFile file, CancellationToken cancellationToken = default) => inner.AddAsync(file, cancellationToken);
        public Task<bool> DeleteAsync(Guid requestId, Guid fileId, CancellationToken cancellationToken = default) => inner.DeleteAsync(requestId, fileId, cancellationToken);
        public Task StageRetentionAsync(Guid requestId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) => inner.StageRetentionAsync(requestId, expiresAt, cancellationToken);
    }
    private InspectaDbContext CreateContext()
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", "admin")], "test"))
            }
        };
        return new InspectaDbContext(options, new CurrentTenant(accessor));
    }

    public void Dispose()
    {
        pdfServices.Dispose();
        connection.Dispose();
    }
}
