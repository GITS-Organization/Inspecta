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
    public async Task WithEmailOnTheCertificatesGoAsAReplyToTheOriginalRequestWithEveryPdfAttached()
    {
        await ApprovedJobAsync(fromEmail: true);

        var preview = await Run(true, service => service.PreviewDeliveryAsync(requestId, coordinator.Id, "coordinator"));
        Assert.True(preview.EmailEnabled);
        Assert.Null(preview.Notice);
        Assert.Equal("buyer@alnoor.com", preview.To);
        Assert.True(preview.RepliesToOriginalEmail);
        Assert.Equal("Re: Inspection request", preview.Subject);
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
        Assert.Equal("Re: Inspection request", email.Subject);
        Assert.Equal("<id@alnoor.com>", email.InReplyTo);
        Assert.Contains("<id@alnoor.com>", email.References);
        Assert.Equal(2, 1 + email.MoreAttachments!.Count);
        Assert.All(email.MoreAttachments.Select(file => file.Content).Append(email.AttachmentContent), content => Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(content, 0, 5)));
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
        Assert.StartsWith("Dear Al Noor,", preview.Body);
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
        public Task<(int Requests, int Items)> CountDependenciesAsync(Guid clientId, CancellationToken cancellationToken = default) => inner.CountDependenciesAsync(clientId, cancellationToken);
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
        Assert.Equal("[TEST] Re: Inspection request", test.Subject);
        var email = Assert.Single(sender.Sent);
        Assert.Equal("coordinator@example.com", email.ToAddress);
        Assert.True(email.Cc is null or { Count: 0 });
        Assert.Equal("[TEST] Re: Inspection request", email.Subject);
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
        Assert.StartsWith("Inspection certificates - Al Noor Industries", preview.Subject);

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
                db.CoordinatorEmailConfigs.Add(CoordinatorEmailConfig.Create(
                    coordinator.Id, manager.Id, "imap.example.com", 993, "coordinator@example.com", "secret", true));
            var items = new[] { RequestItem.Create("Forklift Truck", 1, capacity: "3 ton"), RequestItem.Create("Chain Block", 4) };
            var request = fromEmail
                ? InspectionRequest.CreateFromEmail(manager.Id, coordinator.Id, new IncomingEmail(
                    "42", 1, "Please inspect.", null, "buyer@alnoor.com", "Al Noor", "Inspection request",
                    [], [], [], [], [], "<id@alnoor.com>", null, null, DateTimeOffset.UtcNow.AddMinutes(-5)))
                : InspectionRequest.Create("Al Noor Industries", "Cairo", clientId, items, coordinator.Id, managerId: manager.Id);
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
            new CertificateDeliveryOptions(emailEnabled));
        var service = new MachineInspectionService(
            new EfCertificateRepository(db),
            new EfRequestRepository(db),
            new EfUserRepository(db),
            new EfEquipmentRepository(db),
            new EfClientRepository(db),
            pdfServices.GetRequiredService<IPdfGenerator>(),
            files,
            null,
            delivery);
        return await action(service);
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
