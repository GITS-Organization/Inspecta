using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Inspecta.Application.Certificates;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Contracts.Clients;
using Inspecta.Application.Contracts.Equipment;
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

/// <summary>
/// The inspector's report files on a job (step 1): who may add, see and remove them and when, the limits and the file checks, and
/// what the machine list and the preview ZIP say about them. Real (SQLite) database and repositories; the lists are tested at the service level
/// because the SQLite test host cannot order by a DateTimeOffset in the database.
/// </summary>
public sealed class JobReportTests : IDisposable
{
    private static readonly byte[] Pdf = "%PDF-1.4 a report"u8.ToArray();

    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;
    private readonly ServiceProvider pdfServices;
    private readonly AppUser manager = AppUser.Create("manager@example.com", "Manager Mona", UserRole.TechnicalManager);
    private readonly AppUser otherManager;
    private readonly AppUser coordinator;
    private readonly AppUser otherCoordinator;
    private readonly AppUser inspector;
    private readonly AppUser otherInspector;
    private readonly Guid requestId;
    private readonly Guid forkliftItemId;
    private readonly Guid chainItemId;

    public JobReportTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        otherManager = AppUser.Create("manager2@example.com", "Manager Hany", UserRole.TechnicalManager, managerId: manager.Id);
        coordinator = AppUser.Create("coordinator@example.com", "Coordinator Ali", UserRole.Coordinator, managerId: manager.Id);
        otherCoordinator = AppUser.Create("coordinator2@example.com", "Coordinator Dina", UserRole.Coordinator, managerId: manager.Id);
        inspector = AppUser.Create("inspector@example.com", "Eng. Karim", UserRole.Inspector, managerId: manager.Id);
        otherInspector = AppUser.Create("other@example.com", "Eng. Sara", UserRole.Inspector, managerId: manager.Id);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPdfGeneration(new ConfigurationBuilder().Build());
        pdfServices = services.BuildServiceProvider();

        Guid clientId;
        Guid forkliftEquipmentId;
        using (var db = CreateContext())
        {
            db.Database.EnsureCreated();
            db.Users.AddRange(manager, otherManager, coordinator, otherCoordinator, inspector, otherInspector);
            db.SaveChanges();
        }

        using (var db = CreateContext())
        {
            clientId = new ClientService(new EfClientRepository(db), new EfUserRepository(db)).CreateAsync(new CreateClientRequest
            {
                Name = "Al Noor Industries", Location = "Cairo, Industrial City", CertificatesEmail = "certificates@alnoor.com",
                Contacts = [new CreateClientContactRequest { Name = "Mona Samir", Email = "mona@alnoor.com", IsPrimary = true }]
            }, manager.Id).Result.Id;
            forkliftEquipmentId = new EquipmentService(new EfEquipmentRepository(db)).CreateAsync(new SaveEquipmentRequest
            {
                ClientId = clientId, Name = "Forklift Truck", Manufacturer = "Toyota", SerialNumber = "FK-100"
            }).Result.Id;
        }

        using (var db = CreateContext())
        {
            var forklift = RequestItem.Create("Forklift Truck", 1, forkliftEquipmentId, capacity: "3 ton", serialNumber: "FK-100", manufacturer: "Toyota");
            var chain = RequestItem.Create("Chain Block", 4, capacity: "2 ton");
            var request = InspectionRequest.Create("Al Noor Industries", "Cairo, Industrial City", clientId, [forklift, chain], coordinator.Id, managerId: manager.Id);
            request.UseReviewer(manager.Id);
            request.TransitionTo(JobStatus.QuoteDraft, coordinator.Id);
            request.TransitionTo(JobStatus.QuoteSent, coordinator.Id);
            request.TransitionTo(JobStatus.Unassigned, coordinator.Id);
            request.AssignInspector(inspector.Id, DateTimeOffset.UtcNow.AddDays(2), coordinator.Id);
            db.Requests.Add(request);
            db.SaveChanges();
            requestId = request.Id;
            forkliftItemId = forklift.Id;
            chainItemId = chain.Id;
        }
    }

    // ---- Adding and listing -------------------------------------------------------------------------------------

    [Fact]
    public async Task InspectorUploadsAReportToTheirJobAndSeesItListed()
    {
        await Start();

        var added = await Upload(inspector, "inspector", "Weld report.pdf", Pdf, forkliftItemId);

        Assert.Equal("Weld report.pdf", added.FileName);
        Assert.Equal("application/pdf", added.ContentType);
        Assert.Equal(Pdf.Length, added.SizeBytes);
        Assert.Equal(forkliftItemId, added.RequestItemId);
        Assert.Equal("Forklift Truck", added.MachineName);
        Assert.Equal("Eng. Karim", added.UploadedBy);
        Assert.True(added.CanDelete);
        Assert.Equal($"/api/v1/quotation-queue/{requestId}/reports/{added.Id}", added.DownloadUrl);

        var list = await Reports(service => service.ListAsync(requestId, inspector.Id, "inspector"));
        var file = Assert.Single(list.Files);
        Assert.Equal(added.Id, file.Id);
        Assert.Equal(Pdf.Length, list.TotalBytes);
        Assert.True(list.CanUpload);
        Assert.Equal(20, list.MaxFiles);
        Assert.Equal(10L * 1024 * 1024, list.MaxFileBytes);
        Assert.Equal(30L * 1024 * 1024, list.MaxTotalBytes);
        Assert.Null(list.ExpiresAt);

        var download = await Reports(service => service.DownloadAsync(requestId, added.Id, inspector.Id, "inspector"));
        Assert.Equal(Pdf, download.Content);
        Assert.Equal("Weld report.pdf", download.FileName);
        Assert.Equal("application/pdf", download.ContentType);

        // The technical manager, any technical manager and the admin see the same file.
        foreach (var (user, role) in new[] { (manager, "technical_manager"), (otherManager, "technical_manager") })
            Assert.Single((await Reports(service => service.ListAsync(requestId, user.Id, role))).Files);
    }

    [Fact]
    public async Task AReportForTheJobAsAWholeHasNoMachine()
    {
        await Start();

        var added = await Upload(inspector, "inspector", "photo.jpg", [0xFF, 0xD8, 0xFF, 0xE0, 1, 2]);

        Assert.Null(added.RequestItemId);
        Assert.Null(added.MachineName);
        Assert.Equal("image/jpeg", added.ContentType);
    }

    [Fact]
    public async Task TheMachineListCountsTheReportsAndSaysWhoCanAddMore()
    {
        var before = await Inspection(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector"));
        Assert.Equal(0, before.ReportCount);
        Assert.False(before.CanUploadReports); // the inspection is not started yet

        await Start();
        await Upload(inspector, "inspector", "a.pdf", Pdf);
        await Upload(inspector, "inspector", "b.pdf", Pdf);

        var inspectorList = await Inspection(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector"));
        Assert.Equal(2, inspectorList.ReportCount);
        Assert.True(inspectorList.CanUploadReports);
        Assert.True((await Inspection(service => service.ListMachinesAsync(requestId, manager.Id, "technical_manager"))).CanUploadReports);
        var coordinatorList = await Inspection(service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"));
        Assert.False(coordinatorList.CanUploadReports);
        Assert.Equal(0, coordinatorList.ReportCount); // hidden until approval

        await MoveTo(JobStatus.CertificateSubmitted);
        Assert.False((await Inspection(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector"))).CanUploadReports);
        await MoveTo(JobStatus.CertificateApproved);
        Assert.Equal(2, (await Inspection(service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"))).ReportCount);
    }

    [Fact]
    public async Task UploadForAMachineOfAnotherJobIsRefused()
    {
        await Start();

        var refused = await Assert.ThrowsAsync<ArgumentException>(() => Upload(inspector, "inspector", "a.pdf", Pdf, Guid.NewGuid()));

        Assert.Contains("not part of this job", refused.Message);
        Assert.Empty((await Reports(service => service.ListAsync(requestId, inspector.Id, "inspector"))).Files);
    }

    [Fact]
    public async Task AnotherInspectorAndAnotherCoordinatorDoNotSeeTheReports()
    {
        await Start();
        var added = await Upload(inspector, "inspector", "a.pdf", Pdf);

        foreach (var (user, role) in new[] { (otherInspector, "inspector"), (otherCoordinator, "coordinator") })
        {
            await Assert.ThrowsAsync<KeyNotFoundException>(() => Reports(service => service.ListAsync(requestId, user.Id, role)));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => Reports(service => service.DownloadAsync(requestId, added.Id, user.Id, role)));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => Upload(user, role, "x.pdf", Pdf));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => Reports(service => service.DeleteAsync(requestId, added.Id, user.Id, role)));
        }

        Assert.Single((await Reports(service => service.ListAsync(requestId, inspector.Id, "inspector"))).Files);
    }

    [Fact]
    public async Task CoordinatorCannotUploadOrDeleteAndSeesReportsOnlyAfterApproval()
    {
        await Start();
        var added = await Upload(inspector, "inspector", "a.pdf", Pdf);

        Assert.Equal("forbidden", (await Assert.ThrowsAsync<AuthException>(() => Upload(coordinator, "coordinator", "c.pdf", Pdf))).Code);
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthException>(() => Reports(service => service.DeleteAsync(requestId, added.Id, coordinator.Id, "coordinator")))).StatusCode);
        var early = await Assert.ThrowsAsync<AuthException>(() => Reports(service => service.ListAsync(requestId, coordinator.Id, "coordinator")));
        Assert.Equal("not_approved", early.Code);
        Assert.Equal(409, early.StatusCode);
        Assert.Equal("not_approved", (await Assert.ThrowsAsync<AuthException>(() => Reports(service => service.DownloadAsync(requestId, added.Id, coordinator.Id, "coordinator")))).Code);

        await MoveTo(JobStatus.CertificateSubmitted);
        await MoveTo(JobStatus.CertificateApproved);

        var list = await Reports(service => service.ListAsync(requestId, coordinator.Id, "coordinator"));
        var file = Assert.Single(list.Files);
        Assert.False(file.CanDelete);
        Assert.False(list.CanUpload);
        Assert.Equal(Pdf, (await Reports(service => service.DownloadAsync(requestId, added.Id, coordinator.Id, "coordinator"))).Content);
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthException>(() => Reports(service => service.DeleteAsync(requestId, added.Id, coordinator.Id, "coordinator")))).StatusCode);
    }

    [Fact]
    public async Task AFileOfAnotherJobIsNotFoundOnThisOne()
    {
        await Start();
        var other = await OtherJobFileAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Reports(service => service.DownloadAsync(requestId, other, inspector.Id, "inspector")));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Reports(service => service.DeleteAsync(requestId, other, manager.Id, "technical_manager")));
        await using var db = CreateContext();
        Assert.True(db.JobReportFiles.Any(file => file.Id == other));
    }

    // ---- The file checks ----------------------------------------------------------------------------------------

    [Fact]
    public async Task RenamedProgramZipAndUnknownTypesAreRefused()
    {
        await Start();
        var program = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 3, 0, 0, 0 };
        var elf = new byte[] { 0x7F, 0x45, 0x4C, 0x46, 2, 1, 1 };
        var zip = new byte[] { 0x50, 0x4B, 3, 4, 20, 0 };

        foreach (var (name, content) in new (string, byte[])[]
                 {
                     ("run.exe", program),
                     ("renamed.pdf", program),
                     ("renamed.txt", program),
                     ("linux.csv", elf),
                     ("archive.zip", zip),
                     ("photo.heic", [1, 2, 3, 4]),
                     ("page.html", "<html></html>"u8.ToArray()),
                     ("noextension", Pdf),
                     (".pdf", Pdf),
                     ("fake.pdf", "This is not a pdf"u8.ToArray()),
                     ("fake.png", Pdf),
                     ("fake.docx", Pdf),
                     ("zip-renamed.docx", new byte[] { 0x50, 0x4B, 3, 4, 20, 0 }),
                     ("other-zip.xlsx", OtherZip()),
                     ("empty.pdf", [])
                 })
            await Assert.ThrowsAsync<ArgumentException>(() => Upload(inspector, "inspector", name, content));

        await Assert.ThrowsAsync<ArgumentException>(() => Reports(service => service.UploadAsync(
            requestId, inspector.Id, "inspector", new UploadJobReportRequest { FileName = "a.pdf", ContentBase64 = "not base64!" })));
        Assert.Empty((await Reports(service => service.ListAsync(requestId, inspector.Id, "inspector"))).Files);
    }

    // A minimal Office file: a ZIP that holds the [Content_Types].xml entry every .docx, .xlsx and .pptx has.
    private static byte[] Office() => Zip("[Content_Types].xml");

    // A real ZIP that is not an Office file.
    private static byte[] OtherZip() => Zip("payload.bin");

    private static byte[] Zip(string entryName)
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
            writer.Write("x");
        }

        return stream.ToArray();
    }

    [Fact]
    public async Task EveryAllowedTypeIsAccepted()
    {
        await Start();
        var types = new (string Name, byte[] Content, string ContentType)[]
        {
            ("a.pdf", Pdf, "application/pdf"),
            ("a.png", [0x89, 0x50, 0x4E, 0x47, 1], "image/png"),
            ("a.JPG", [0xFF, 0xD8, 0xFF, 1], "image/jpeg"),
            ("a.jpeg", [0xFF, 0xD8, 0xFF, 1], "image/jpeg"),
            ("a.gif", "GIF89a"u8.ToArray(), "image/gif"),
            ("a.txt", "Notes"u8.ToArray(), "text/plain"),
            ("a.csv", "a,b"u8.ToArray(), "text/csv"),
            ("MZ-123 notes.txt", "MZ-123 inspection notes"u8.ToArray(), "text/plain"),
            ("a.doc", [0xD0, 0xCF, 0x11, 0xE0, 1], "application/msword"),
            ("a.docx", Office(), "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
            ("a.xls", [0xD0, 0xCF, 0x11, 0xE0, 1], "application/vnd.ms-excel"),
            ("a.xlsx", Office(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
            ("a.ppt", [0xD0, 0xCF, 0x11, 0xE0, 1], "application/vnd.ms-powerpoint"),
            ("a.pptx", Office(), "application/vnd.openxmlformats-officedocument.presentationml.presentation")
        };

        foreach (var (name, content, contentType) in types)
            Assert.Equal(contentType, (await Upload(inspector, "inspector", name, content)).ContentType);
    }

    [Fact]
    public async Task PathInFileNameIsRemoved()
    {
        await Start();

        var windows = await Upload(inspector, "inspector", @"..\..\a.pdf", Pdf);
        var unix = await Upload(inspector, "inspector", "../../etc/b.pdf", Pdf);
        var control = await Upload(inspector, "inspector", "c\r\n.pdf", Pdf);

        Assert.Equal("a.pdf", windows.FileName);
        Assert.Equal("b.pdf", unix.FileName);
        Assert.Equal("c.pdf", control.FileName);
        await using var db = CreateContext();
        Assert.Equal(["a.pdf", "b.pdf", "c.pdf"], db.JobReportFiles.Select(file => file.FileName).AsEnumerable().Order().ToArray());
        await Assert.ThrowsAsync<ArgumentException>(() => Upload(inspector, "inspector", new string('x', 148) + ".pdf", Pdf));
    }

    [Fact]
    public async Task TooLargeFileTooManyFilesOrTooMuchPerJobIsRefused()
    {
        await Start();

        var tooLarge = new byte[10 * 1024 * 1024 + 1];
        Pdf.CopyTo(tooLarge, 0);
        await Assert.ThrowsAsync<ArgumentException>(() => Upload(inspector, "inspector", "huge.pdf", tooLarge));
        // The text alone is already too long to be a file of the limit: refused before it is decoded.
        await Assert.ThrowsAsync<ArgumentException>(() => Reports(service => service.UploadAsync(
            requestId, inspector.Id, "inspector", new UploadJobReportRequest { FileName = "huge.pdf", ContentBase64 = new string('A', 15_000_000) })));

        // 10 MB exactly is fine; three of 9 MB fill 27 of the 30 MB, so another 4 MB does not fit.
        var nine = new byte[9 * 1024 * 1024];
        Pdf.CopyTo(nine, 0);
        for (var number = 1; number <= 3; number++) await Upload(inspector, "inspector", $"big{number}.pdf", nine);
        var full = await Assert.ThrowsAsync<AuthException>(() => Upload(inspector, "inspector", "four.pdf", new byte[4 * 1024 * 1024].Select((_, index) => index < Pdf.Length ? Pdf[index] : (byte)0).ToArray()));
        Assert.Equal("report_limit_reached", full.Code);
        Assert.Equal(409, full.StatusCode);
        await Upload(inspector, "inspector", "three.pdf", new byte[3 * 1024 * 1024].Select((_, index) => index < Pdf.Length ? Pdf[index] : (byte)0).ToArray());
    }

    [Fact]
    public async Task TheTwentyFirstFileIsRefused()
    {
        await Start();
        for (var number = 1; number <= 20; number++) await Upload(inspector, "inspector", $"r{number}.pdf", Pdf);

        var full = await Assert.ThrowsAsync<AuthException>(() => Upload(inspector, "inspector", "r21.pdf", Pdf));

        Assert.Equal("report_limit_reached", full.Code);
        Assert.Equal(20, (await Reports(service => service.ListAsync(requestId, inspector.Id, "inspector"))).Files.Count);
        // Removing one makes room again.
        var first = (await Reports(service => service.ListAsync(requestId, inspector.Id, "inspector"))).Files[0];
        await Reports(service => service.DeleteAsync(requestId, first.Id, inspector.Id, "inspector"));
        await Upload(inspector, "inspector", "r21.pdf", Pdf);
    }

    // ---- Locking and removing -----------------------------------------------------------------------------------

    [Fact]
    public async Task ReportsCanOnlyBeAddedWhileTheInspectorWorks()
    {
        var early = await Assert.ThrowsAsync<AuthException>(() => Upload(inspector, "inspector", "a.pdf", Pdf));
        Assert.Equal("reports_locked", early.Code);
        Assert.Equal(409, early.StatusCode);

        await Start();
        await Upload(inspector, "inspector", "a.pdf", Pdf);
        await Upload(manager, "technical_manager", "m.pdf", Pdf);
        await Upload(otherManager, "technical_manager", "m2.pdf", Pdf);

        await MoveTo(JobStatus.CertificateSubmitted);
        Assert.Equal("reports_locked", (await Assert.ThrowsAsync<AuthException>(() => Upload(inspector, "inspector", "b.pdf", Pdf))).Code);
        Assert.Equal("reports_locked", (await Assert.ThrowsAsync<AuthException>(() => Upload(manager, "technical_manager", "b.pdf", Pdf))).Code);
    }

    [Fact]
    public async Task InspectorDeletesOwnFileBeforeSubmitButNotAfter()
    {
        await Start();
        var first = await Upload(inspector, "inspector", "first.pdf", Pdf);
        var second = await Upload(inspector, "inspector", "second.pdf", Pdf);
        var managerFile = await Upload(manager, "technical_manager", "mine.pdf", Pdf);

        var afterDelete = await Reports(service => service.DeleteAsync(requestId, first.Id, inspector.Id, "inspector"));
        Assert.DoesNotContain(afterDelete.Files, file => file.Id == first.Id);
        Assert.Equal(2, afterDelete.Files.Count);
        // The inspector's list says which files are theirs to remove.
        Assert.True(afterDelete.Files.Single(file => file.Id == second.Id).CanDelete);
        Assert.False(afterDelete.Files.Single(file => file.Id == managerFile.Id).CanDelete);
        Assert.Equal("forbidden", (await Assert.ThrowsAsync<AuthException>(() => Reports(service => service.DeleteAsync(requestId, managerFile.Id, inspector.Id, "inspector")))).Code);

        await SubmitBothMachines();

        var locked = await Assert.ThrowsAsync<AuthException>(() => Reports(service => service.DeleteAsync(requestId, second.Id, inspector.Id, "inspector")));
        Assert.Equal("reports_locked", locked.Code);
        Assert.Equal(409, locked.StatusCode);
        Assert.Equal("reports_locked", (await Assert.ThrowsAsync<AuthException>(() => Upload(inspector, "inspector", "late.pdf", Pdf))).Code);
        Assert.False((await Reports(service => service.ListAsync(requestId, inspector.Id, "inspector"))).Files.Any(file => file.CanDelete));

        await Inspection(service => service.ReturnToInspectorAsync(requestId, manager.Id, "technical_manager", new ReturnJobRequest { Note = "Add the weld photos" }));

        await Upload(inspector, "inspector", "photos.pdf", Pdf);
        var after = await Reports(service => service.DeleteAsync(requestId, second.Id, inspector.Id, "inspector"));
        Assert.DoesNotContain(after.Files, file => file.Id == second.Id);
        await using var db = CreateContext();
        Assert.False(db.JobReportFiles.Any(file => file.Id == second.Id));
        Assert.False(db.Set<JobReportContent>().Any(content => content.JobReportFileId == second.Id)); // the bytes went too
    }

    [Fact]
    public async Task ReviewerRemovesAReportWhileReviewingOtherTechnicalManagerCannot()
    {
        await Start();
        var added = await Upload(inspector, "inspector", "a.pdf", Pdf);
        var second = await Upload(inspector, "inspector", "b.pdf", Pdf);
        await MoveTo(JobStatus.CertificateSubmitted);

        var refused = await Assert.ThrowsAsync<AuthException>(() => Reports(service => service.DeleteAsync(requestId, added.Id, otherManager.Id, "technical_manager")));
        Assert.Equal("not_the_reviewer", refused.Code);
        Assert.Equal(403, refused.StatusCode);
        Assert.Equal(2, (await Reports(service => service.ListAsync(requestId, otherManager.Id, "technical_manager"))).Files.Count(file => !file.CanDelete));

        var list = await Reports(service => service.DeleteAsync(requestId, added.Id, manager.Id, "technical_manager"));
        Assert.Equal(second.Id, Assert.Single(list.Files).Id);
        Assert.True(list.Files[0].CanDelete);

        // The reviewer can also remove after approval, and the admin at any time.
        await MoveTo(JobStatus.CertificateApproved);
        var admin = await Reports(service => service.DeleteAsync(requestId, second.Id, Guid.NewGuid(), "admin"));
        Assert.Empty(admin.Files);
    }

    [Fact]
    public async Task RemovingAMachineKeepsItsReportOnTheJob()
    {
        await Start();
        var added = await Upload(inspector, "inspector", "chain.pdf", Pdf, chainItemId);
        Assert.Equal("Chain Block", added.MachineName);

        await Machines(service => service.RemoveAsync(requestId, chainItemId, inspector.Id, "inspector"));

        var file = Assert.Single((await Reports(service => service.ListAsync(requestId, inspector.Id, "inspector"))).Files);
        Assert.Equal(added.Id, file.Id);
        Assert.Null(file.RequestItemId);
        Assert.Null(file.MachineName);
    }

    [Fact]
    public async Task AnExpiredFileIsGoneEverywhereBeforeTheCleanupRuns()
    {
        await Start();
        var kept = await Upload(inspector, "inspector", "kept.pdf", Pdf);
        var gone = await Upload(inspector, "inspector", "gone.pdf", Pdf);
        await using (var db = CreateContext())
        {
            db.JobReportFiles.Single(file => file.Id == gone.Id).StartRetention(DateTimeOffset.UtcNow.AddMinutes(-1));
            db.JobReportFiles.Single(file => file.Id == kept.Id).StartRetention(DateTimeOffset.UtcNow.AddDays(3));
            await db.SaveChangesAsync();
        }

        var list = await Reports(service => service.ListAsync(requestId, manager.Id, "technical_manager"));

        Assert.Equal(kept.Id, Assert.Single(list.Files).Id);
        Assert.NotNull(list.ExpiresAt);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Reports(service => service.DownloadAsync(requestId, gone.Id, manager.Id, "technical_manager")));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Reports(service => service.DeleteAsync(requestId, gone.Id, manager.Id, "technical_manager")));
        Assert.Equal(1, (await Inspection(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector"))).ReportCount);
    }

    // ---- The preview ZIP ----------------------------------------------------------------------------------------

    [Fact]
    public async Task PreviewZipHasAReportsFolder()
    {
        await Start();
        await Inspection(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector", CompleteForm("forklift")));

        var without = await Inspection(service => service.GetPreviewZipAsync(requestId, inspector.Id, "inspector"));
        Assert.DoesNotContain(EntryNames(without.Content), name => name.StartsWith("reports/"));

        await Upload(inspector, "inspector", "Weld report.pdf", Pdf, forkliftItemId);
        await Upload(inspector, "inspector", "Weld report.pdf", Pdf);
        await Upload(manager, "technical_manager", "notes.txt", "Check the brake."u8.ToArray());

        foreach (var (user, role) in new[] { (inspector, "inspector"), (manager, "technical_manager") })
        {
            var zip = await Inspection(service => service.GetPreviewZipAsync(requestId, user.Id, role));
            var names = EntryNames(zip.Content);
            Assert.Contains(names, name => name.EndsWith(".pdf") && !name.StartsWith("reports/"));
            Assert.Contains("reports/Weld report.pdf", names);
            Assert.Contains("reports/Weld report (2).pdf", names);
            Assert.Contains("reports/notes.txt", names);
        }

        // The report is in the ZIP byte for byte.
        using var archive = new ZipArchive(new MemoryStream((await Inspection(service => service.GetPreviewZipAsync(requestId, inspector.Id, "inspector"))).Content));
        using var entry = archive.GetEntry("reports/notes.txt")!.Open();
        using var text = new StreamReader(entry);
        Assert.Equal("Check the brake.", await text.ReadToEndAsync());
    }

    // ---- All the reports in one ZIP (step 2) -------------------------------------------------------------------------

    [Fact]
    public async Task EveryLiveReportComesInOneZipForThoseWhoCanSeeThem()
    {
        await Start();
        await Upload(inspector, "inspector", "Weld report.pdf", Pdf, forkliftItemId);
        await Upload(inspector, "inspector", "Weld report.pdf", Pdf);
        await Upload(manager, "technical_manager", "notes.txt", "Check the brake."u8.ToArray());
        var gone = await Upload(inspector, "inspector", "gone.pdf", Pdf);
        await using (var db = CreateContext())
        {
            db.JobReportFiles.Single(file => file.Id == gone.Id).StartRetention(DateTimeOffset.UtcNow.AddMinutes(-1));
            await db.SaveChangesAsync();
        }

        foreach (var (user, role) in new[] { (inspector, "inspector"), (manager, "technical_manager"), (otherManager, "technical_manager"), (inspector, "admin") })
        {
            var zip = await Reports(service => service.DownloadAllAsync(requestId, user.Id, role));

            Assert.Equal("application/zip", zip.ContentType);
            Assert.StartsWith("reports-Al-Noor-Industries", zip.FileName);
            Assert.EndsWith(".zip", zip.FileName);
            Assert.Equal(["reports/Weld report.pdf", "reports/Weld report (2).pdf", "reports/notes.txt"], EntryNames(zip.Content));
        }

        // The report is in the ZIP byte for byte.
        using var archive = new ZipArchive(new MemoryStream((await Reports(service => service.DownloadAllAsync(requestId, manager.Id, "technical_manager"))).Content));
        using var entry = archive.GetEntry("reports/notes.txt")!.Open();
        using var text = new StreamReader(entry);
        Assert.Equal("Check the brake.", await text.ReadToEndAsync());
    }

    [Fact]
    public async Task TheZipIsNotFoundWhenThereAreNoLiveReportsAndFollowsTheSameAccessRulesAsTheList()
    {
        await Start();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Reports(service => service.DownloadAllAsync(requestId, inspector.Id, "inspector")));

        var added = await Upload(inspector, "inspector", "a.pdf", Pdf);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Reports(service => service.DownloadAllAsync(requestId, otherInspector.Id, "inspector")));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Reports(service => service.DownloadAllAsync(requestId, otherCoordinator.Id, "coordinator")));

        // The owning coordinator has it from approval only.
        var early = await Assert.ThrowsAsync<AuthException>(() => Reports(service => service.DownloadAllAsync(requestId, coordinator.Id, "coordinator")));
        Assert.Equal("not_approved", early.Code);
        Assert.Equal(409, early.StatusCode);
        await MoveTo(JobStatus.CertificateSubmitted);
        await MoveTo(JobStatus.CertificateApproved);
        Assert.Equal(["reports/a.pdf"], EntryNames((await Reports(service => service.DownloadAllAsync(requestId, coordinator.Id, "coordinator"))).Content));

        // After the 30 days nothing is left to download, even before the clean-up runs.
        await using (var db = CreateContext())
        {
            db.JobReportFiles.Single(file => file.Id == added.Id).StartRetention(DateTimeOffset.UtcNow.AddMinutes(-1));
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Reports(service => service.DownloadAllAsync(requestId, coordinator.Id, "coordinator")));
    }

    // ---- Settings -----------------------------------------------------------------------------------------------

    [Fact]
    public void TheRetentionIs30DaysByDefaultAndStaysBetween1And365()
    {
        Assert.Equal(30, new JobReportOptions().RetentionDays);
        Assert.Equal(7, new JobReportOptions(7).RetentionDays);
        Assert.Equal(1, new JobReportOptions(0).RetentionDays);
        Assert.Equal(365, new JobReportOptions(100_000).RetentionDays);
        Assert.Equal(TimeSpan.FromDays(30), new JobReportOptions().Retention);
    }

    // ---- Setup --------------------------------------------------------------------------------------------------

    private static IReadOnlyList<string> EntryNames(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip));
        return archive.Entries.Select(entry => entry.FullName).ToArray();
    }

    private Task<JobReportFileResponse> Upload(AppUser user, string role, string name, byte[] content, Guid? requestItemId = null) =>
        Reports(service => service.UploadAsync(requestId, user.Id, role,
            new UploadJobReportRequest { FileName = name, ContentBase64 = Convert.ToBase64String(content), RequestItemId = requestItemId }));

    private async Task<Guid> OtherJobFileAsync()
    {
        await using var db = CreateContext();
        var other = InspectionRequest.Create("Other Ltd", "Giza", null, [RequestItem.Create("Mobile crane", 1)], coordinator.Id, managerId: manager.Id);
        db.Requests.Add(other);
        var file = JobReportFile.Create(manager.Id, other.Id, null, inspector.Id, "Eng. Karim", "other.pdf", "application/pdf", Pdf);
        db.JobReportFiles.Add(file);
        await db.SaveChangesAsync();
        return file.Id;
    }

    private async Task Start() => await Inspection(service => service.StartInspectionAsync(requestId, inspector.Id, "inspector"));

    private async Task MoveTo(JobStatus status)
    {
        await using var db = CreateContext();
        var request = db.Requests.Include(value => value.Items).Include(value => value.History).Single(value => value.Id == requestId);
        request.TransitionTo(status, manager.Id);
        await db.SaveChangesAsync();
    }

    private async Task SubmitBothMachines()
    {
        foreach (var (item, key) in new[] { (forkliftItemId, "forklift"), (chainItemId, "basic") })
            await Inspection(service => service.SaveFormAsync(requestId, item, inspector.Id, "inspector", CompleteForm(key)));
        await Inspection(service => service.SubmitAllAsync(requestId, inspector.Id, "inspector"));
    }

    private static SaveMachineFormRequest CompleteForm(string templateKey)
    {
        var template = CertificateTemplateCatalog.Find(templateKey)!;
        return new SaveMachineFormRequest
        {
            Fields = template.Fields.Where(field => field.Prefill is null).ToDictionary(field => field.Key, _ => "n/a"),
            Checklist = template.ChecklistPaths.ToDictionary(path => path, _ => new ChecklistAnswerRequest { Value = "yes" }),
            Answers = new Dictionary<string, bool>
            {
                ["firstExamination"] = false, ["within12Months"] = true, ["examinationScheme"] = true,
                ["exceptionalCircumstances"] = false, ["existingDanger"] = false, ["futureDanger"] = false
            },
            Texts = new Dictionary<string, string> { ["testType"] = "Visual and function" },
            Dates = new Dictionary<string, DateOnly> { ["examination"] = new(2026, 11, 5), ["nextExamination"] = new(2027, 11, 5) },
            Result = InspectionResult.Satisfactory
        };
    }

    private async Task<T> Reports<T>(Func<JobReportService, Task<T>> action)
    {
        await using var db = CreateContext();
        return await action(new JobReportService(new EfRequestRepository(db), new EfJobReportRepository(db), new EfUserRepository(db)));
    }

    private MachineInspectionService InspectionService(InspectaDbContext db) => new(
        new EfCertificateRepository(db), new EfRequestRepository(db), new EfUserRepository(db), new EfEquipmentRepository(db),
        new EfClientRepository(db), pdfServices.GetRequiredService<IPdfGenerator>(), new EfCertificateFileRepository(db),
        qrCodes: new Inspecta.Infrastructure.Services.QrCodeRenderer(), transactions: new EfTransactionRunner(db),
        jobReports: new EfJobReportRepository(db));

    private async Task<T> Inspection<T>(Func<MachineInspectionService, Task<T>> action)
    {
        await using var db = CreateContext();
        return await action(InspectionService(db));
    }

    private async Task<T> Machines<T>(Func<JobMachineService, Task<T>> action)
    {
        await using var db = CreateContext();
        var catalog = new EfEquipmentCatalog(db);
        return await action(new JobMachineService(
            new EfRequestRepository(db), new EfCertificateRepository(db), catalog, new RequestEquipmentRegistrar(catalog),
            new EquipmentService(new EfEquipmentRepository(db)), InspectionService(db)));
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
