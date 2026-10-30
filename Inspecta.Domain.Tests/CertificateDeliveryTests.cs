using System.IO.Compression;
using System.Security.Claims;
using Inspecta.Application.Certificates;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Contracts.Clients;
using Inspecta.Application.Exceptions;
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
        Assert.Contains("Forklift Truck (CERT-", preview.Body);
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
        Guid clientId;
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
            Dates = new Dictionary<string, DateOnly> { ["examination"] = new(2026, 11, 5), ["nextExamination"] = new(2027, 11, 5) }
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
