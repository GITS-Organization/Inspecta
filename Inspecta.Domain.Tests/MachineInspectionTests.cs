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
/// An assigned job from the inspector's first call to the coordinator handing the certificates over, run against a
/// real (SQLite) database with the real repositories and the real PDF generator.
/// </summary>
public sealed class MachineInspectionTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<InspectaDbContext> options;
    private readonly ServiceProvider pdfServices;
    private readonly AppUser manager = AppUser.Create("manager@example.com", "Manager Mona", UserRole.TechnicalManager);
    private readonly AppUser coordinator;
    private readonly AppUser inspector;
    private readonly AppUser otherInspector;
    private readonly Guid clientId;
    private readonly Guid forkliftEquipmentId;
    private readonly Guid requestId;
    private readonly Guid forkliftItemId;
    private readonly Guid chainItemId;

    public MachineInspectionTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<InspectaDbContext>().UseSqlite(connection).Options;
        coordinator = AppUser.Create("coordinator@example.com", "Coordinator Ali", UserRole.Coordinator, managerId: manager.Id);
        inspector = AppUser.Create("inspector@example.com", "Eng. Karim", UserRole.Inspector, managerId: manager.Id);
        inspector.SetQualifications([new UserQualification("FOU"), new UserQualification("LAC")]);
        otherInspector = AppUser.Create("other@example.com", "Eng. Sara", UserRole.Inspector, managerId: manager.Id);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPdfGeneration(new ConfigurationBuilder().Build());
        pdfServices = services.BuildServiceProvider();

        using (var db = CreateContext())
        {
            db.Database.EnsureCreated();
            db.Users.AddRange(manager, coordinator, inspector, otherInspector);
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
                ClientId = clientId, Name = "Forklift Truck", Manufacturer = "Toyota", SerialNumber = "FK-100", TestType = "Visual & function test"
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

    [Fact]
    public async Task StartingTheInspectionAcceptsTheTaskAndListsTheMachines()
    {
        var before = await Run(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector"));
        Assert.True(before.CanStartInspection);
        Assert.All(before.Machines, machine => Assert.False(machine.CanEdit));
        Assert.False((await Run(service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"))).CanStartInspection);

        var list = await Run(service => service.StartInspectionAsync(requestId, inspector.Id, "inspector"));

        Assert.Equal(JobStatus.InProgress, list.RequestStatus);
        Assert.Equal(2, list.Machines.Count);
        var forklift = list.Machines.Single(machine => machine.Name == "Forklift Truck");
        Assert.Equal("forklift", forklift.TemplateKey);
        Assert.Equal(MachineStatus.NotStarted, forklift.Status);
        Assert.Equal(35, forklift.ChecklistTotal);
        var chain = list.Machines.Single(machine => machine.Name == "Chain Block");
        Assert.Equal("basic", chain.TemplateKey);
        Assert.Equal(0, chain.ChecklistTotal);
        Assert.Equal(new MachineProgress(2, 0, 0, 0), list.Progress);
        Assert.False(list.CanStartInspection);
        Assert.False(list.CanDeliver);
        Assert.All(list.Machines, machine => Assert.True(machine.CanEdit));
        Assert.All(list.Machines, machine => Assert.False(machine.CanReview));

        var again = await Run(service => service.StartInspectionAsync(requestId, inspector.Id, "inspector"));
        Assert.Equal(JobStatus.InProgress, again.RequestStatus);
    }

    [Fact]
    public async Task OnlyTheAssignedInspectorSeesAndStartsTheJob()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Run(service => service.StartInspectionAsync(requestId, otherInspector.Id, "inspector")));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Run(service => service.ListMachinesAsync(requestId, otherInspector.Id, "inspector")));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Run(service => service.GetFormAsync(requestId, forkliftItemId, otherInspector.Id, "inspector")));
        var denied = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.StartInspectionAsync(requestId, coordinator.Id, "coordinator")));
        Assert.Equal(403, denied.StatusCode);
    }

    // The same JSON options as the API (camelCase, snake_case enums).
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower) }
    };

    [Fact]
    public void TheSaveBodyTheFrontendSendsIsUnderstood()
    {
        const string body = """
            { "fields": { "ndt": "MT" },
              "checklist": { "tires_and_wheels.sidewalls_cracks": { "value": "no", "note": "Cut" } },
              "answers": { "firstExamination": false },
              "texts": { "testType": "Visual" },
              "dates": { "examination": "2026-11-05", "nextExamination": "2027-11-05" } }
            """;

        var request = System.Text.Json.JsonSerializer.Deserialize<SaveMachineFormRequest>(body, Json)!;

        Assert.Equal("MT", request.Fields!["ndt"]);
        Assert.Equal("no", request.Checklist!["tires_and_wheels.sidewalls_cracks"].Value);
        Assert.Equal("Cut", request.Checklist["tires_and_wheels.sidewalls_cracks"].Note);
        Assert.False(request.Answers!["firstExamination"]);
        Assert.Equal(new DateOnly(2027, 11, 5), request.Dates!["nextExamination"]);
        Assert.Null(System.Text.Json.JsonSerializer.Deserialize<SaveMachineFormRequest>("{}", Json)!.Fields);
    }

    [Fact]
    public async Task TheFormJsonUsesReadableNames()
    {
        await Start();

        var form = await Run(service => service.GetFormAsync(requestId, forkliftItemId, inspector.Id, "inspector"));
        var json = System.Text.Json.JsonSerializer.Serialize(form, Json);

        Assert.Contains("\"requestStatus\":\"in_progress\"", json);
        Assert.Contains("\"status\":\"not_started\"", json);
        Assert.Contains("\"canEdit\":true", json);
        Assert.Contains("\"readyToSubmit\":false", json);
        Assert.Contains("\"templateKey\":\"forklift\"", json);
        var list = System.Text.Json.JsonSerializer.Serialize(await Run(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector")), Json);
        Assert.Contains("\"canStartInspection\":false", list);
        Assert.Contains("\"requestItemId\":\"" + forkliftItemId, list);
    }

    [Fact]
    public async Task TheFormHasTheTemplateTheMachinesOwnDetailsAndTheStandards()
    {
        await Start();

        var form = await Run(service => service.GetFormAsync(requestId, forkliftItemId, inspector.Id, "inspector"));

        Assert.Equal("forklift", form.TemplateKey);
        Assert.True(form.CanEdit);
        Assert.Equal(MachineStatus.NotStarted, form.Status);
        Assert.Equal("Al Noor Industries", form.Header.ClientName);
        Assert.Equal("Cairo, Industrial City", form.Header.ClientAddress);
        Assert.Equal("Mona Samir", form.Header.ClientRepresentative);
        Assert.Equal(["BS EN ISO 3691-1:2011", "BS ISO 2330:2002"], form.Header.Standards);
        Assert.Equal("Toyota", form.Fields.Single(field => field.Key == "manufacturer").Value);
        Assert.Equal("3 ton", form.Fields.Single(field => field.Key == "swl").Value);
        Assert.Equal("FK-100", form.Fields.Single(field => field.Key == "serialNumber").Value);
        Assert.Null(form.Fields.Single(field => field.Key == "ndt").Value);
        Assert.Equal(6, form.Checklist.Count);
        Assert.Equal(["yes", "no", "na"], form.ChecklistValues);
        Assert.Equal(JobStatus.InProgress, form.RequestStatus);
        // The registered item's test type starts the certificate's Test type (the inspector can change it).
        Assert.Equal("Visual & function test", form.Texts["testType"]);
        Assert.False(form.ReadyToSubmit);
        Assert.Contains("the examination date", form.Missing);
        Assert.Contains("35 checklist row(s)", form.Missing);

        var chain = await Run(service => service.GetFormAsync(requestId, chainItemId, inspector.Id, "inspector"));
        Assert.Equal("basic", chain.TemplateKey);
        Assert.Equal("4", chain.Fields.Single(field => field.Key == "quantity").Value);
        Assert.Equal("Chain Block", chain.Fields.Single(field => field.Key == "description").Value);
        Assert.Equal(["BS EN 13157:2004 +A1:2009"], chain.Header.Standards);
        Assert.Empty(chain.Checklist);
    }

    [Fact]
    public async Task SavingNeedsTheInspectionToHaveStartedAndKnownKeys()
    {
        var early = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.SaveFormAsync(
            requestId, chainItemId, inspector.Id, "inspector", new SaveMachineFormRequest())));
        Assert.Equal("inspection_not_started", early.Code);

        await Start();
        await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.SaveFormAsync(
            requestId, chainItemId, inspector.Id, "inspector", new SaveMachineFormRequest { Fields = new Dictionary<string, string> { ["spanLength"] = "9" } })));
        await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.SaveFormAsync(
            requestId, forkliftItemId, inspector.Id, "inspector", new SaveMachineFormRequest
            {
                Checklist = new Dictionary<string, ChecklistAnswerRequest> { ["nope.nope"] = new() { Value = "yes" } }
            })));
        await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.SaveFormAsync(
            requestId, forkliftItemId, inspector.Id, "inspector", new SaveMachineFormRequest
            {
                Checklist = new Dictionary<string, ChecklistAnswerRequest> { [FirstRow("forklift")] = new() { Value = "maybe" } }
            })));
        await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.SaveFormAsync(
            requestId, chainItemId, inspector.Id, "inspector", new SaveMachineFormRequest { Answers = new Dictionary<string, bool> { ["bogus"] = true } })));
        await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.SaveFormAsync(
            requestId, chainItemId, inspector.Id, "inspector", new SaveMachineFormRequest
            {
                Answers = new Dictionary<string, bool> { ["within6Months"] = true, ["within12Months"] = true }
            })));
    }

    [Fact]
    public async Task ASavedDraftComesBackAndTheQuestionnaireAnswersAreDerived()
    {
        await Start();

        var saved = await Run(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector", new SaveMachineFormRequest
        {
            Fields = new Dictionary<string, string> { ["ndt"] = "MT, no cracks" },
            Checklist = new Dictionary<string, ChecklistAnswerRequest> { [FirstRow("forklift")] = new() { Value = "YES" } },
            Answers = new Dictionary<string, bool> { ["firstExamination"] = false, ["within12Months"] = true },
            Texts = new Dictionary<string, string> { ["testType"] = "Visual" }
        }));

        Assert.Equal(MachineStatus.Draft, saved.Status);
        Assert.Equal("MT, no cracks", saved.Fields.Single(field => field.Key == "ndt").Value);
        Assert.Equal("yes", saved.Checklist.SelectMany(section => section.Items).Single(row => row.Key == FirstRow("forklift")).Value);
        Assert.False(saved.Answers["within6Months"]);
        Assert.True(saved.Answers["within12Months"]);

        var reopened = await Run(service => service.GetFormAsync(requestId, forkliftItemId, inspector.Id, "inspector"));
        Assert.Equal("Visual", reopened.Texts["testType"]);
        var list = await Run(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector"));
        Assert.Equal(1, list.Machines.Single(machine => machine.Name == "Forklift Truck").ChecklistAnswered);
    }

    [Fact]
    public async Task TheFormSaysWhenTheMachineIsReadyToSubmitAndDatesAreDatesNotTimestamps()
    {
        await Start();

        var saved = await Run(service => service.SaveFormAsync(requestId, chainItemId, inspector.Id, "inspector", CompleteForm("basic")));

        Assert.True(saved.ReadyToSubmit);
        Assert.Empty(saved.Missing);
        Assert.Equal(new DateOnly(2026, 11, 5), saved.Dates["examination"]);
        Assert.Equal(new DateOnly(2027, 11, 5), saved.Dates["nextExamination"]);
        var json = System.Text.Json.JsonSerializer.Serialize(saved.Dates);
        Assert.Contains("\"2026-11-05\"", json);
    }

    [Fact]
    public async Task AnIncompleteCertificateCannotBeSubmittedAndTheMessageSaysWhatIsMissing()
    {
        await Start();
        await Run(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector", new SaveMachineFormRequest
        {
            Checklist = new Dictionary<string, ChecklistAnswerRequest> { [FirstRow("forklift")] = new() { Value = "no" } }
        }));

        var incomplete = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.SubmitAsync(requestId, forkliftItemId, inspector.Id, "inspector")));

        Assert.Equal("certificate_incomplete", incomplete.Code);
        Assert.Equal(409, incomplete.StatusCode);
        Assert.Contains("examination date", incomplete.Message);
        Assert.Contains("34 checklist row(s)", incomplete.Message);
        Assert.Contains("a note for 1 checklist row(s) answered No", incomplete.Message);
        Assert.Contains("the description of the defect", incomplete.Message);
        Assert.Contains("the result (satisfactory or unsatisfactory)", incomplete.Problems!);
        Assert.Contains("the examination date", incomplete.Problems!);
        Assert.DoesNotContain("the conclusion", incomplete.Problems!);

        var nothing = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.SubmitAsync(requestId, chainItemId, inspector.Id, "inspector")));
        Assert.Equal("nothing_to_submit", nothing.Code);
    }

    [Fact]
    public async Task TheWholeJobFromInspectionToDeliveryToTheClient()
    {
        await Start();
        await Run(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector", CompleteForm("forklift")));
        await Run(service => service.SaveFormAsync(requestId, chainItemId, inspector.Id, "inspector", CompleteForm("basic")));

        var submitted = await Run(service => service.SubmitAsync(requestId, forkliftItemId, inspector.Id, "inspector"));
        Assert.Equal(MachineStatus.Submitted, submitted.Status);
        Assert.False(submitted.CanEdit);
        Assert.Equal(JobStatus.InProgress, (await Run(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector"))).RequestStatus);
        await Assert.ThrowsAsync<AuthException>(() => Run(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector", new SaveMachineFormRequest())));
        await Run(service => service.SubmitAsync(requestId, chainItemId, inspector.Id, "inspector"));

        var review = await Run(service => service.ListMachinesAsync(requestId, manager.Id, "technical_manager"));
        Assert.Equal(JobStatus.CertificateSubmitted, review.RequestStatus);
        Assert.All(review.Machines, machine => Assert.True(machine.CanReview));
        Assert.All(review.Machines, machine => Assert.False(machine.CanEdit));
        Assert.All((await Run(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector"))).Machines, machine => Assert.False(machine.CanReview));
        Assert.Equal(new MachineProgress(2, 2, 0, 0), review.Progress);
        var preparedBy = (await Run(service => service.GetFormAsync(requestId, forkliftItemId, manager.Id, "technical_manager"))).Texts;
        Assert.Equal("Eng. Karim", preparedBy["preparedByName"]);
        Assert.Equal("FOU, LAC", preparedBy["preparedByQualifications"]);
        // The verdict is the result now; a blank conclusion stays blank instead of becoming "Satisfactory".
        Assert.False(preparedBy.ContainsKey("conclusion"));
        Assert.Equal("NONE", preparedBy["defectDescription"]);

        // The manager sends one machine back and approves the other.
        await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.ReturnAsync(requestId, chainItemId, manager.Id, "technical_manager", " ")));
        var returned = await Run(service => service.ReturnAsync(requestId, chainItemId, manager.Id, "technical_manager", "Serial numbers are missing"));
        Assert.Equal(MachineStatus.Returned, returned.Status);
        Assert.Equal("Serial numbers are missing", returned.ReviewNote);
        Assert.True(returned.CanEdit);
        var approved = await Run(service => service.ApproveAsync(requestId, forkliftItemId, manager.Id, "technical_manager"));
        Assert.Equal(MachineStatus.Approved, approved.Status);
        Assert.Equal($"CERT-{DateTimeOffset.UtcNow.Year}-0001", approved.CertificateNumber);
        Assert.Equal(JobStatus.CertificateReturned, (await Run(service => service.ListMachinesAsync(requestId, manager.Id, "technical_manager"))).RequestStatus);

        // The inspector fixes it and submits again; the manager approves; the job is ready for the coordinator.
        await Run(service => service.SaveFormAsync(requestId, chainItemId, inspector.Id, "inspector", new SaveMachineFormRequest
        {
            Fields = new Dictionary<string, string> { ["serialNumber"] = "CB-1, CB-2, CB-3, CB-4" }
        }));
        await Run(service => service.SubmitAsync(requestId, chainItemId, inspector.Id, "inspector"));
        Assert.Equal(JobStatus.CertificateSubmitted, (await Run(service => service.ListMachinesAsync(requestId, manager.Id, "technical_manager"))).RequestStatus);
        var secondApproved = await Run(service => service.ApproveAsync(requestId, chainItemId, manager.Id, "technical_manager"));
        Assert.Equal($"CERT-{DateTimeOffset.UtcNow.Year}-0002", secondApproved.CertificateNumber);
        var ready = await Run(service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"));
        Assert.Equal(JobStatus.CertificateApproved, ready.RequestStatus);
        Assert.Equal(new MachineProgress(2, 2, 2, 0), ready.Progress);
        Assert.True(ready.CanDeliver);
        Assert.False((await Run(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector"))).CanDeliver);

        // The approved machine updated its own record.
        await using (var db = CreateContext())
        {
            var equipment = await new EfEquipmentRepository(db).GetByIdAsync(forkliftEquipmentId, tracking: false);
            Assert.Equal(new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero), equipment!.LastInspectionAt);
            Assert.Equal(new DateTimeOffset(2027, 11, 5, 0, 0, 0, TimeSpan.Zero), equipment.NextInspectionDueAt);
        }

        // Only the coordinator side delivers; the client's certificates email is recorded.
        await Assert.ThrowsAsync<AuthException>(() => Run(service => service.DeliverAsync(requestId, inspector.Id, "inspector", new DeliverCertificatesRequest())));
        var delivered = await Run(service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest()));
        Assert.Equal(JobStatus.SentToClient, delivered.RequestStatus);
        Assert.All(delivered.Machines, machine => Assert.Equal(MachineStatus.Delivered, machine.Status));
        await using var check = CreateContext();
        Assert.All(check.Certificates.AsNoTracking().ToList(), certificate => Assert.Equal("certificates@alnoor.com", certificate.SentTo));
    }

    [Fact]
    public async Task ApprovingFilesTheFinalPdfUnderTheClientAndServesExactlyThatFile()
    {
        await Start();
        await Run(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector", CompleteForm("forklift")));
        await Run(service => service.SubmitAsync(requestId, forkliftItemId, inspector.Id, "inspector"));
        await Run(service => service.SaveFormAsync(requestId, chainItemId, inspector.Id, "inspector", CompleteForm("basic")));
        await Run(service => service.SubmitAsync(requestId, chainItemId, inspector.Id, "inspector"));

        var approved = await Run(service => service.ApproveAsync(requestId, forkliftItemId, manager.Id, "technical_manager"));

        await using var db = CreateContext();
        var file = Assert.Single(db.IssuedCertificateFiles.AsNoTracking().Include(value => value.Body).ToList());
        Assert.Equal(approved.CertificateNumber, file.CertificateNumber);
        Assert.Equal(clientId, file.ClientId);
        Assert.Equal("Al Noor Industries", file.ClientName);
        Assert.Equal("Forklift Truck", file.ItemName);
        Assert.Equal(new DateOnly(2026, 11, 5), file.ExaminationDate);
        Assert.Equal(new DateOnly(2027, 11, 5), file.NextExaminationDate);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(file.Content, 0, 5));
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file.Content)).ToLowerInvariant(), file.Sha256);
        Assert.Equal(16, file.VerificationCode.Length);
        var certificate = db.Certificates.AsNoTracking().Single(value => value.RequestItemId == forkliftItemId);
        Assert.Equal(file.VerificationCode, certificate.VerificationCode);
        Assert.Equal($"https://api.gitsplatform.com/verify/{file.VerificationCode}", certificate.QrData);
        // The archive keeps what the printed QR opens, so it can also be sent to the client as an image.
        Assert.Equal(certificate.QrData, file.QrData);
        Assert.Equal(forkliftEquipmentId, file.EquipmentId);
        Assert.Equal("Eng. Karim", file.PreparedBy);

        var served = await Run(service => service.GetPdfAsync(requestId, forkliftItemId, coordinator.Id, "coordinator"));
        Assert.Equal(file.Content, served.Content);
        Assert.Equal(file.FileName, served.FileName);
        var again = await Run(service => service.GetPdfAsync(requestId, forkliftItemId, manager.Id, "technical_manager"));
        Assert.Equal(file.Content, again.Content);

        // The client's archive and the public lookup see it without the PDF being loaded.
        await using var read = CreateContext();
        var repository = new EfCertificateFileRepository(read);
        var listed = Assert.Single(await repository.ListForClientAsync(clientId));
        Assert.Equal(file.CertificateNumber, listed.CertificateNumber);
        Assert.Equal(file.QrData, listed.QrData);
        Assert.Equal(file.CertificateNumber, (await repository.FindByVerificationCodeAsync(file.VerificationCode))!.CertificateNumber);
        Assert.Null(await repository.FindByVerificationCodeAsync("NOPE"));
        // A job's files can be described (with their size) without reading the PDFs.
        var described = Assert.Single(await repository.ListSummariesForRequestAsync(requestId));
        Assert.Equal((file.CertificateNumber, file.Content.Length), (described.CertificateNumber, described.ContentLength));
        // The PDF sits in its own table: a plain read of the certificate row never brings it along.
        Assert.Throws<InvalidOperationException>(() => read.IssuedCertificateFiles.AsNoTracking().First(value => value.Id == file.Id).Content);
        Assert.Equal(file.Content, (await repository.GetForClientAsync(clientId, file.Id))!.Content);
        Assert.Null(await repository.GetForClientAsync(Guid.NewGuid(), file.Id));

        // The machine's next certificate prints this one as its "Last Certificate No./Made by".
        var previous = await repository.FindLatestForEquipmentAsync(forkliftEquipmentId, Guid.NewGuid());
        Assert.Equal((file.CertificateNumber, "Eng. Karim", file.ExaminationDate), (previous!.CertificateNumber, previous.PreparedBy, previous.ExaminationDate));
        Assert.Null(await repository.FindLatestForEquipmentAsync(forkliftEquipmentId, file.CertificateId));
        Assert.Null(await repository.FindLatestForEquipmentAsync(Guid.NewGuid(), Guid.NewGuid()));

        var archive = await new ClientCertificateService(new EfClientRepository(read), repository).ListAsync(clientId);
        Assert.Equal(file.QrData, Assert.Single(archive).QrUrl);
    }

    [Fact]
    public async Task TheManagerSendsTheJobBackThenApprovesItWholeAndTheQrCodesGoWithTheCertificates()
    {
        await Start();
        foreach (var (item, key) in new[] { (forkliftItemId, "forklift"), (chainItemId, "basic") })
        {
            await Run(service => service.SaveFormAsync(requestId, item, inspector.Id, "inspector", CompleteForm(key)));
            await Run(service => service.SubmitAsync(requestId, item, inspector.Id, "inspector"));
        }

        // Before approving, the technical manager can look at every certificate of the job in one ZIP; the coordinator cannot.
        var preview = await Run(service => service.GetPreviewZipAsync(requestId, manager.Id, "technical_manager"));
        using (var drafts = new System.IO.Compression.ZipArchive(new MemoryStream(preview.Content)))
            Assert.Equal(["01-Chain-Block.pdf", "02-Forklift-Truck.pdf"], drafts.Entries.Select(entry => entry.FullName).Order().ToArray());
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthException>(() => Run(service => service.GetPreviewZipAsync(requestId, coordinator.Id, "coordinator")))).StatusCode);

        // The inspector sees the certificate as it will look, and cannot review it.
        Assert.Equal("%PDF-", Encoding.ASCII.GetString((await Run(service => service.GetPdfAsync(requestId, forkliftItemId, inspector.Id, "inspector"))).Content, 0, 5));
        Assert.Equal(403, (await Assert.ThrowsAsync<AuthException>(() => Run(service => service.ApproveAllAsync(requestId, inspector.Id, "inspector")))).StatusCode);
        await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.ReturnToInspectorAsync(requestId, manager.Id, "technical_manager", new ReturnJobRequest())));

        var returned = await Run(service => service.ReturnToInspectorAsync(requestId, manager.Id, "technical_manager", new ReturnJobRequest { Note = "Check the dates" }));
        Assert.Equal(JobStatus.CertificateReturned, returned.RequestStatus);
        Assert.All(returned.Machines, machine => Assert.Equal(MachineStatus.Returned, machine.Status));
        Assert.All(returned.Machines, machine => Assert.Equal("Check the dates", machine.ReviewNote));
        Assert.Equal(409, (await Assert.ThrowsAsync<AuthException>(() => Run(service => service.ApproveAllAsync(requestId, manager.Id, "technical_manager")))).StatusCode);

        foreach (var item in new[] { forkliftItemId, chainItemId })
            await Run(service => service.SubmitAsync(requestId, item, inspector.Id, "inspector"));
        var approved = await Run(service => service.ApproveAllAsync(requestId, manager.Id, "technical_manager"));
        Assert.Equal(JobStatus.CertificateApproved, approved.RequestStatus);
        Assert.All(approved.Machines, machine => Assert.Equal(MachineStatus.Approved, machine.Status));
        Assert.True(approved.CanDeliver);

        var qr = await Run(service => service.GetQrCodeAsync(requestId, forkliftItemId, coordinator.Id, "coordinator"));
        Assert.Equal("image/png", qr.ContentType);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], qr.Content.Take(4).ToArray());

        var zip = await Run(service => service.GetCertificatesZipAsync(requestId, coordinator.Id, "coordinator"));
        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(zip.Content));
        Assert.Equal(2, archive.Entries.Count(entry => entry.FullName.EndsWith(".pdf", StringComparison.Ordinal)));
        Assert.Equal(2, archive.Entries.Count(entry => entry.FullName.StartsWith("qr/", StringComparison.Ordinal)));

        await using var db = CreateContext();
        var file = db.IssuedCertificateFiles.AsNoTracking().First();
        var archived = await new ClientCertificateService(new EfClientRepository(db), new EfCertificateFileRepository(db), new Inspecta.Infrastructure.Services.QrCodeRenderer())
            .GetQrCodeAsync(clientId, file.Id);
        Assert.Equal("image/png", archived.ContentType);
    }

    [Fact]
    public async Task TheJobIsReviewedAsAWholeWithCommentsPerMachineAndKeepsItsQr()
    {
        await Start();
        await Run(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector", CompleteForm("forklift")));
        await Run(service => service.SubmitAsync(requestId, forkliftItemId, inspector.Id, "inspector"));

        // Half a job cannot be decided on.
        Assert.Equal("not_under_review", (await Assert.ThrowsAsync<AuthException>(() => Run(service => service.ApproveAllAsync(requestId, manager.Id, "technical_manager")))).Code);
        Assert.Equal("not_under_review", (await Assert.ThrowsAsync<AuthException>(() => Run(service => service.ReturnToInspectorAsync(
            requestId, manager.Id, "technical_manager", new ReturnJobRequest { Note = "Fix" })))).Code);

        await Run(service => service.SaveFormAsync(requestId, chainItemId, inspector.Id, "inspector", CompleteForm("basic")));
        await Run(service => service.SubmitAsync(requestId, chainItemId, inspector.Id, "inspector"));
        string reserved;
        await using (var db = CreateContext())
        {
            // The QR is made on submit, so the preview carries it; the number waits for approval.
            var submitted = db.Certificates.AsNoTracking().Single(value => value.RequestItemId == forkliftItemId);
            reserved = submitted.VerificationCode!;
            Assert.Equal($"https://api.gitsplatform.com/verify/{reserved}", submitted.QrData);
            Assert.Null(submitted.CertificateNumber);
        }

        var listed = await Run(service => service.ListMachinesAsync(requestId, manager.Id, "technical_manager"));
        Assert.True(listed.CanReview);
        Assert.All(listed.Machines, machine => Assert.Equal(InspectionResult.Satisfactory, machine.Result));

        var unknownMachine = await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.ReturnToInspectorAsync(requestId, manager.Id, "technical_manager",
            new ReturnJobRequest { Note = "Fix", Machines = [new MachineReviewComment { RequestItemId = Guid.NewGuid(), Comment = "x" }] })));
        Assert.Contains("is not part of this job", unknownMachine.Message);
        var returned = await Run(service => service.ReturnToInspectorAsync(requestId, manager.Id, "technical_manager", new ReturnJobRequest
        {
            Note = "Please fix and resubmit",
            Machines = [new MachineReviewComment { RequestItemId = chainItemId, Comment = "Serial number spelled wrong" }]
        }));
        Assert.Equal(JobStatus.CertificateReturned, returned.RequestStatus);
        Assert.False(returned.CanReview);
        Assert.All(returned.Machines, machine => Assert.Equal(MachineStatus.Returned, machine.Status));
        Assert.Equal("Please fix and resubmit", returned.Machines.Single(machine => machine.RequestItemId == forkliftItemId).ReviewNote);
        Assert.Equal("Serial number spelled wrong\nPlease fix and resubmit", returned.Machines.Single(machine => machine.RequestItemId == chainItemId).ReviewNote);
        Assert.Equal(2, returned.ReviewHistory!.Count);
        Assert.Contains(returned.ReviewHistory, comment => comment.RequestItemId == chainItemId && comment.MachineName == "Chain Block" && comment.By == "Manager Mona");
        Assert.Contains(returned.ReviewHistory, comment => comment.RequestItemId is null && comment.Text == "Please fix and resubmit");

        foreach (var item in new[] { forkliftItemId, chainItemId })
        {
            await Run(service => service.SaveFormAsync(requestId, item, inspector.Id, "inspector", new SaveMachineFormRequest { Texts = new Dictionary<string, string> { ["note"] = "Fixed" } }));
            await Run(service => service.SubmitAsync(requestId, item, inspector.Id, "inspector"));
        }

        var approved = await Run(service => service.ApproveAllAsync(requestId, manager.Id, "technical_manager"));
        Assert.Equal(JobStatus.CertificateApproved, approved.RequestStatus);
        Assert.Equal(2, approved.ReviewHistory!.Count);
        await using var check = CreateContext();
        Assert.Equal(reserved, check.Certificates.AsNoTracking().Single(value => value.RequestItemId == forkliftItemId).VerificationCode);
    }

    [Fact]
    public async Task ApprovingAJobWithoutItsCertificatesIsRefusedWithTheReasonsAndChangesNothing()
    {
        await MoveToReviewWithoutCertificates();

        var listed = await Run(service => service.ListMachinesAsync(requestId, manager.Id, "technical_manager"));
        Assert.True(listed.CanReview);
        Assert.False(listed.CanApprove);
        Assert.Equal(
            ["Chain Block: the inspector has not filled in its certificate", "Forklift Truck: the inspector has not filled in its certificate"],
            listed.ReviewBlockers!);

        var refused = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.ApproveAllAsync(requestId, manager.Id, "technical_manager")));
        Assert.Equal("job_not_ready", refused.Code);
        Assert.Equal(409, refused.StatusCode);
        Assert.Equal(listed.ReviewBlockers, refused.Problems);
        await using var db = CreateContext();
        Assert.Equal(JobStatus.CertificateSubmitted, db.Requests.AsNoTracking().Single(value => value.Id == requestId).Status);
        Assert.Empty(db.IssuedCertificateFiles.AsNoTracking().ToList());
        Assert.Empty(db.CertificateCounters.AsNoTracking().ToList());
    }

    [Fact]
    public async Task AJobThatReachedReviewWithoutCertificatesCanBeSentBackSoTheInspectorFinishesIt()
    {
        await MoveToReviewWithoutCertificates();

        var returned = await Run(service => service.ReturnToInspectorAsync(requestId, manager.Id, "technical_manager", new ReturnJobRequest
        {
            Note = "Fill in both certificates",
            Machines = [new MachineReviewComment { RequestItemId = forkliftItemId, Comment = "Add the load test" }]
        }));

        Assert.Equal(JobStatus.CertificateReturned, returned.RequestStatus);
        Assert.False(returned.CanReview);
        Assert.Equal(2, returned.ReviewHistory!.Count);
        Assert.Contains(returned.ReviewHistory, comment => comment.MachineName == "Forklift Truck" && comment.Text == "Add the load test");

        // The inspector fills the machines in and submits them; the job is back in review and can be approved.
        foreach (var (item, key) in new[] { (forkliftItemId, "forklift"), (chainItemId, "basic") })
        {
            await Run(service => service.SaveFormAsync(requestId, item, inspector.Id, "inspector", CompleteForm(key)));
            await Run(service => service.SubmitAsync(requestId, item, inspector.Id, "inspector"));
        }

        Assert.True((await Run(service => service.ListMachinesAsync(requestId, manager.Id, "technical_manager"))).CanApprove);
        Assert.Equal(JobStatus.CertificateApproved, (await Run(service => service.ApproveAllAsync(requestId, manager.Id, "technical_manager"))).RequestStatus);
    }

    [Fact]
    public async Task ApprovingIsAllOrNothingAndNoCertificateNumberIsUsedUp()
    {
        await SubmitBothMachines();

        // The second PDF cannot be drawn: the first machine must not stay approved.
        await Assert.ThrowsAsync<PdfGenerationException>(() => RunWith(
            new FailingOnCall(pdfServices.GetRequiredService<IPdfGenerator>(), failOn: 2),
            service => service.ApproveAllAsync(requestId, manager.Id, "technical_manager")));

        await using (var db = CreateContext())
        {
            Assert.Equal(JobStatus.CertificateSubmitted, db.Requests.AsNoTracking().Single(value => value.Id == requestId).Status);
            Assert.All(db.Certificates.AsNoTracking().ToList(), certificate => Assert.Null(certificate.CertificateNumber));
            Assert.Empty(db.IssuedCertificateFiles.AsNoTracking().ToList());
            Assert.All(db.CertificateCounters.AsNoTracking().ToList(), counter => Assert.Equal(0, counter.LastNumber));
        }

        // Trying again approves everything, and the numbering starts at 1: nothing was wasted.
        var approved = await Run(service => service.ApproveAllAsync(requestId, manager.Id, "technical_manager"));
        Assert.Equal(JobStatus.CertificateApproved, approved.RequestStatus);
        Assert.Equal(["CERT-" + DateTimeOffset.UtcNow.Year + "-0001", "CERT-" + DateTimeOffset.UtcNow.Year + "-0002"],
            approved.Machines.Select(machine => machine.CertificateNumber!).Order().ToArray());
    }

    [Fact]
    public async Task ASignatureThatIsNotStrokesIsRefusedBeforeAnythingIsChanged()
    {
        await SubmitBothMachines();

        var invalid = await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.ApproveAllAsync(
            requestId, manager.Id, "technical_manager", new ApproveMachineRequest { Signature = "data:image/png;base64,iVBORw0KGgo=" })));
        Assert.Contains("JSON array of strokes", invalid.Message);
        await using var db = CreateContext();
        Assert.Equal(JobStatus.CertificateSubmitted, db.Requests.AsNoTracking().Single(value => value.Id == requestId).Status);
        Assert.Empty(db.CertificateCounters.AsNoTracking().ToList());
    }

    [Fact]
    public async Task TheReviewScreenGetsWhatIsPrintedOnEachCertificateInOneCall()
    {
        await SubmitBothMachines();

        var list = await Run(service => service.ListMachinesAsync(requestId, manager.Id, "technical_manager"));

        Assert.True(list.CanReview);
        Assert.True(list.CanApprove);
        Assert.Empty(list.ReviewBlockers!);
        Assert.Equal($"/api/v1/quotation-queue/{requestId}/certificates/preview.zip", list.PreviewZipUrl);
        var forklift = list.Machines.Single(machine => machine.RequestItemId == forkliftItemId);
        Assert.Equal("Forklift Truck", forklift.Type);
        Assert.Equal("Forklift Truck", forklift.TemplateName);
        Assert.Equal("Toyota", forklift.Manufacturer);
        Assert.Equal("3 ton", forklift.Capacity);
        Assert.Equal(["BS EN ISO 3691-1:2011", "BS ISO 2330:2002"], forklift.InspectionStandards!);
        Assert.Equal(new DateOnly(2026, 11, 5), forklift.ExaminationDate);
        Assert.Equal(new DateOnly(2027, 11, 5), forklift.NextExaminationDate);
        Assert.Equal($"/api/v1/quotation-queue/{requestId}/machines/{forkliftItemId}/pdf", forklift.PdfUrl);
        Assert.StartsWith("https://api.gitsplatform.com/verify/", forklift.QrUrl);
        Assert.Equal(InspectionResult.Satisfactory, forklift.Result);
        var chain = list.Machines.Single(machine => machine.RequestItemId == chainItemId);
        Assert.Equal("Chain Block", chain.Type);
        Assert.Equal("2 ton", chain.Capacity);

        // The inspector gets the data too, but not the review buttons.
        var forInspector = await Run(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector"));
        Assert.False(forInspector.CanReview);
        Assert.False(forInspector.CanApprove);
        Assert.Null(forInspector.PreviewZipUrl);
    }

    [Fact]
    public async Task TheResultIsRequiredAndAnUnsatisfactoryMachineIsStillCertified()
    {
        await Start();
        var withoutResult = CompleteForm("basic") with { Result = null };
        await Run(service => service.SaveFormAsync(requestId, chainItemId, inspector.Id, "inspector", withoutResult));
        var missing = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.SubmitAsync(requestId, chainItemId, inspector.Id, "inspector")));
        Assert.Equal(["the result (satisfactory or unsatisfactory)"], missing.Problems!);

        var form = await Run(service => service.SaveFormAsync(requestId, chainItemId, inspector.Id, "inspector",
            new SaveMachineFormRequest { Result = InspectionResult.Unsatisfactory }));
        Assert.Equal(InspectionResult.Unsatisfactory, form.Result);
        Assert.Equal(["the description of the defect"], form.Missing);
        await Run(service => service.SaveFormAsync(requestId, chainItemId, inspector.Id, "inspector", new SaveMachineFormRequest
        {
            Texts = new Dictionary<string, string> { ["defectDescription"] = "Worn load chain", ["conclusion"] = "Not to be used until the chain is replaced" }
        }));
        await Run(service => service.SubmitAsync(requestId, chainItemId, inspector.Id, "inspector"));
        await Run(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector", CompleteForm("forklift")));
        await Run(service => service.SubmitAsync(requestId, forkliftItemId, inspector.Id, "inspector"));

        var approved = await Run(service => service.ApproveAllAsync(requestId, manager.Id, "technical_manager"));
        Assert.All(approved.Machines, machine => Assert.Equal(MachineStatus.Approved, machine.Status));
        Assert.Equal(InspectionResult.Unsatisfactory, approved.Machines.Single(machine => machine.RequestItemId == chainItemId).Result);

        await using var db = CreateContext();
        var files = db.IssuedCertificateFiles.AsNoTracking().ToList();
        Assert.Equal(CertificateResult.NotSafe, files.Single(file => file.ItemName == "Chain Block").Result);
        Assert.Equal(CertificateResult.SafeToOperate, files.Single(file => file.ItemName == "Forklift Truck").Result);
        Assert.Equal(CertificateResult.SafeToOperate, db.Items.AsNoTracking().Single(item => item.Id == forkliftEquipmentId).LastInspectionResult);
        var archive = await new ClientCertificateService(new EfClientRepository(db), new EfCertificateFileRepository(db)).ListAsync(clientId);
        Assert.Contains(archive, row => row.ItemName == "Chain Block" && row.Result == InspectionResult.Unsatisfactory);
    }

    [Fact]
    public async Task CertificateNumbersCountUpPerTeamAndYear()
    {
        await using var db = CreateContext();
        var repository = new EfCertificateFileRepository(db);
        var team = Guid.NewGuid();

        Assert.Equal("CERT-2026-0001", await repository.NextCertificateNumberAsync(team, 2026));
        Assert.Equal("CERT-2026-0002", await repository.NextCertificateNumberAsync(team, 2026));
        Assert.Equal("CERT-2027-0001", await repository.NextCertificateNumberAsync(team, 2027));
        Assert.Equal("CERT-2026-0001", await repository.NextCertificateNumberAsync(Guid.NewGuid(), 2026));

        // A second connection to the same database continues the count instead of starting over.
        await using var other = CreateContext();
        Assert.Equal("CERT-2026-0003", await new EfCertificateFileRepository(other).NextCertificateNumberAsync(team, 2026));
    }

    [Fact]
    public async Task TheManagersSignatureIsPrintedAndCanBeRememberedForTheNextApprovals()
    {
        const string signature = "[[0.1,0.2,0.3,0.4,0.5,0.6]]";
        await Start();
        foreach (var (item, key) in new[] { (forkliftItemId, "forklift"), (chainItemId, "basic") })
        {
            await Run(service => service.SaveFormAsync(requestId, item, inspector.Id, "inspector", CompleteForm(key)));
            await Run(service => service.SubmitAsync(requestId, item, inspector.Id, "inspector"));
        }

        await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.ApproveAsync(
            requestId, forkliftItemId, manager.Id, "technical_manager", new ApproveMachineRequest { Signature = "not json" })));
        await Run(service => service.ApproveAsync(
            requestId, forkliftItemId, manager.Id, "technical_manager", new ApproveMachineRequest { Signature = signature, RememberSignature = true }));
        await Run(service => service.ApproveAsync(requestId, chainItemId, manager.Id, "technical_manager"));

        await using var db = CreateContext();
        Assert.All(db.Certificates.AsNoTracking().ToList(), certificate => Assert.Equal(signature, certificate.SignatureStrokesJson));
        Assert.Equal(signature, db.Users.AsNoTracking().Single(user => user.Id == manager.Id).SignatureStrokesJson);
    }

    [Fact]
    public async Task OnlyTheTechnicalManagerReviewsAndOnlyASubmittedCertificate()
    {
        await Start();
        await Run(service => service.SaveFormAsync(requestId, chainItemId, inspector.Id, "inspector", CompleteForm("basic")));
        await Run(service => service.SubmitAsync(requestId, chainItemId, inspector.Id, "inspector"));

        var notYet = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.ApproveAsync(requestId, chainItemId, manager.Id, "technical_manager")));
        Assert.Equal("not_under_review", notYet.Code);
        foreach (var role in new[] { "inspector", "coordinator" })
        {
            var denied = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.ApproveAsync(requestId, chainItemId, inspector.Id, role)));
            Assert.Equal(403, denied.StatusCode);
        }

        var early = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.DeliverAsync(requestId, coordinator.Id, "coordinator", new DeliverCertificatesRequest())));
        Assert.Equal("not_approved", early.Code);
    }

    [Fact]
    public async Task ANoAnswerNeedsANoteAndAnExplanationBeforeItCanBeSubmitted()
    {
        await Start();
        var form = CompleteForm("forklift");
        var row = FirstRow("forklift");
        var checklist = new Dictionary<string, ChecklistAnswerRequest>(form.Checklist!)
        {
            [row] = new() { Value = "no", Note = "Cut on the rear-left tyre" }
        };
        await Run(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector", form with { Checklist = checklist }));

        var needsText = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.SubmitAsync(requestId, forkliftItemId, inspector.Id, "inspector")));
        Assert.DoesNotContain("checklist row(s) answered No", needsText.Message);
        Assert.Contains("the description of the defect", needsText.Message);

        await Run(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector", new SaveMachineFormRequest
        {
            Texts = new Dictionary<string, string> { ["defectDescription"] = "Cut tyre", ["conclusion"] = "Not satisfactory until the tyre is replaced" }
        }));
        var submitted = await Run(service => service.SubmitAsync(requestId, forkliftItemId, inspector.Id, "inspector"));
        Assert.Equal(MachineStatus.Submitted, submitted.Status);
        Assert.Equal("Cut on the rear-left tyre", submitted.Checklist.SelectMany(section => section.Items).Single(item => item.Key == row).Note);
    }

    [Fact]
    public async Task TheCoordinatorGetsTheCertificatePdfOnlyOnceItIsApprovedAndOthersGetADraft()
    {
        await Start();
        await Run(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector", CompleteForm("forklift")));
        await Run(service => service.SaveFormAsync(requestId, chainItemId, inspector.Id, "inspector", CompleteForm("basic")));

        var draft = await Run(service => service.GetPdfAsync(requestId, forkliftItemId, inspector.Id, "inspector"));
        Assert.StartsWith("certificate-", draft.FileName);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(draft.Content, 0, 5));
        Assert.True(PageCount(draft.Content) >= 2, "the forklift certificate has a checklist page");
        var chainDraft = await Run(service => service.GetPdfAsync(requestId, chainItemId, manager.Id, "technical_manager"));
        Assert.Equal(1, PageCount(chainDraft.Content));

        var notReady = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.GetPdfAsync(requestId, forkliftItemId, coordinator.Id, "coordinator")));
        Assert.Equal("not_approved", notReady.Code);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Run(service => service.GetPdfAsync(requestId, forkliftItemId, otherInspector.Id, "inspector")));

        await Run(service => service.SubmitAsync(requestId, forkliftItemId, inspector.Id, "inspector"));
        await Run(service => service.SubmitAsync(requestId, chainItemId, inspector.Id, "inspector"));
        await Run(service => service.ApproveAsync(requestId, forkliftItemId, manager.Id, "technical_manager"));
        var approved = await Run(service => service.GetPdfAsync(requestId, forkliftItemId, coordinator.Id, "coordinator"));
        Assert.True(approved.Content.Length > 1000);
    }

    [Fact]
    public async Task TheQuotationListReadsOnlyTheStateOfACertificateNotItsFormData()
    {
        await using (var db = CreateContext())
        {
            db.Certificates.Add(InspectionCertificate.Create(requestId, inspector.Id, requestItemId: chainItemId));
            await db.SaveChangesAsync();
        }

        await using var read = CreateContext();
        var states = await new EfQuoteRepository(read).ListCertificateStatesAsync([requestId]);

        var state = Assert.Single(states[requestId]);
        Assert.Equal(chainItemId, state.RequestItemId);
        Assert.Equal(MachineStatus.Draft, MachineInspectionService.StatusOf(state));
        Assert.Empty(await new EfQuoteRepository(read).ListCertificateStatesAsync([Guid.NewGuid()]));
        Assert.Empty(read.ChangeTracker.Entries<InspectionCertificate>()); // nothing was loaded as an entity
    }

    [Fact]
    public async Task AnOlderCertificateWithoutATemplateIsLeftAlone()
    {
        await using (var db = CreateContext())
        {
            db.Certificates.Add(InspectionCertificate.Create(requestId, inspector.Id, requestItemId: chainItemId));
            await db.SaveChangesAsync();
        }

        await Start();

        var blocked = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.GetFormAsync(requestId, chainItemId, inspector.Id, "inspector")));
        Assert.Equal("legacy_certificate", blocked.Code);
    }

    // ---- Helpers ------------------------------------------------------------------------------------------------

    // ---- The coordinator sets the number and the date of each certificate before the handover -------------------------

    private static UpdateCertificateDetailsRequest Details(params (Guid Item, string Number, DateOnly Date)[] rows) =>
        new() { Certificates = rows.Select(row => new CertificateDetail { RequestItemId = row.Item, CertificateNumber = row.Number, CertificateDate = row.Date }).ToArray() };

    private async Task ApproveTheJob()
    {
        await SubmitBothMachines();
        await Run(service => service.ApproveAllAsync(requestId, manager.Id, "technical_manager"));
    }

    [Fact]
    public async Task TheCoordinatorSetsEachCertificatesNumberAndDateAndTheFilesAreDrawnAgain()
    {
        await ApproveTheJob();
        var before = await Run(service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"));
        Assert.True(before.CanEditCertificateDetails);
        var forklift = before.Machines.Single(machine => machine.RequestItemId == forkliftItemId);
        var examination = forklift.ExaminationDate;
        Assert.NotNull(forklift.CertificateDate);
        string firstHash;
        await using (var db = CreateContext())
            firstHash = db.IssuedCertificateFiles.IgnoreQueryFilters().Single(file => file.RequestId == requestId && file.CertificateNumber == forklift.CertificateNumber).Sha256;

        var list = await Run(service => service.UpdateCertificateDetailsAsync(requestId, coordinator.Id, "coordinator",
            Details((forkliftItemId, "CERT-2026-0100", new DateOnly(2026, 10, 30)), (chainItemId, " CERT-2026-0101 ", new DateOnly(2026, 10, 29)))));

        var changed = list.Machines.Single(machine => machine.RequestItemId == forkliftItemId);
        Assert.Equal("CERT-2026-0100", changed.CertificateNumber);
        Assert.Equal(new DateOnly(2026, 10, 30), changed.CertificateDate);
        Assert.Equal("CERT-2026-0101", list.Machines.Single(machine => machine.RequestItemId == chainItemId).CertificateNumber);
        Assert.Equal(examination, changed.ExaminationDate); // the inspector's examination date is left as he wrote it
        await using var check = CreateContext();
        var file = check.IssuedCertificateFiles.IgnoreQueryFilters().Single(value => value.RequestId == requestId && value.CertificateNumber == "CERT-2026-0100");
        Assert.NotEqual(firstHash, file.Sha256);
        Assert.Contains("CERT-2026-0100", file.FileName);
        Assert.Equal(new DateOnly(2026, 10, 30), DateOnly.FromDateTime(file.IssuedAt.UtcDateTime));
        Assert.Equal(2, check.IssuedCertificateFiles.IgnoreQueryFilters().Count(value => value.RequestId == requestId)); // replaced, not added

        // The same pair can be saved again, and the coordinator can still read each certificate as a PDF.
        await Run(service => service.UpdateCertificateDetailsAsync(requestId, coordinator.Id, "coordinator",
            Details((forkliftItemId, "CERT-2026-0100", new DateOnly(2026, 10, 30)))));
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString((await Run(service => service.GetPdfAsync(requestId, forkliftItemId, coordinator.Id, "coordinator"))).Content, 0, 4));
    }

    [Fact]
    public async Task ANumberAlreadyUsedABadDateOrALockedJobIsRefusedAndNothingChanges()
    {
        await ApproveTheJob();
        var list = await Run(service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"));
        var chainNumber = list.Machines.Single(machine => machine.RequestItemId == chainItemId).CertificateNumber!;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.UpdateCertificateDetailsAsync(requestId, coordinator.Id, "coordinator",
            Details((forkliftItemId, "X-1", today), (chainItemId, "x-1", today)))));
        await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.UpdateCertificateDetailsAsync(requestId, coordinator.Id, "coordinator",
            Details((forkliftItemId, "  ", today)))));
        await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.UpdateCertificateDetailsAsync(requestId, coordinator.Id, "coordinator",
            Details((forkliftItemId, "X-2", new DateOnly(2019, 12, 31))))));
        await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.UpdateCertificateDetailsAsync(requestId, coordinator.Id, "coordinator",
            Details((forkliftItemId, "X-3", today.AddDays(5))))));
        var taken = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.UpdateCertificateDetailsAsync(requestId, coordinator.Id, "coordinator",
            Details((forkliftItemId, chainNumber, today)))));
        Assert.Equal("certificate_number_taken", taken.Code);
        var inspectorDenied = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.UpdateCertificateDetailsAsync(requestId, inspector.Id, "inspector",
            Details((forkliftItemId, "X-4", today)))));
        Assert.Equal(403, inspectorDenied.StatusCode);
        var unchanged = await Run(service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"));
        Assert.Equal(list.Machines.Select(machine => machine.CertificateNumber), unchanged.Machines.Select(machine => machine.CertificateNumber));

        // Once the certificates are handed to the client they are final.
        await Run(service => service.DeliverManuallyAsync(requestId, coordinator.Id, "coordinator", new ManualDeliveryRequest()));
        var locked = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.UpdateCertificateDetailsAsync(requestId, coordinator.Id, "coordinator",
            Details((forkliftItemId, "X-5", today)))));
        Assert.Equal("not_approved", locked.Code);
        Assert.False((await Run(service => service.ListMachinesAsync(requestId, coordinator.Id, "coordinator"))).CanEditCertificateDetails);
    }

    // ---- Submit every certificate of the job together ---------------------------------------------------------------

    [Fact]
    public async Task EveryCertificateOfTheJobIsSubmittedTogetherAndTheJobGoesToReview()
    {
        await Start();
        await Run(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector", CompleteForm("forklift")));
        await Run(service => service.SaveFormAsync(requestId, chainItemId, inspector.Id, "inspector", CompleteForm("basic")));

        var list = await Run(service => service.SubmitAllAsync(requestId, inspector.Id, "inspector"));

        Assert.Equal(JobStatus.CertificateSubmitted, list.RequestStatus);
        Assert.All(list.Machines, machine => Assert.Equal(MachineStatus.Submitted, machine.Status));
        Assert.All(list.Machines, machine => Assert.NotNull(machine.QrUrl)); // the QR was made on submit
        var review = await Run(service => service.ListMachinesAsync(requestId, manager.Id, "technical_manager"));
        Assert.True(review.CanApprove);
    }

    [Fact]
    public async Task SubmittingEverythingIsAllOrNothingAndNamesWhatIsMissing()
    {
        await Start();
        await Run(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector", CompleteForm("forklift")));

        var refused = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.SubmitAllAsync(requestId, inspector.Id, "inspector")));

        Assert.Equal("certificate_incomplete", refused.Code);
        Assert.Equal(409, refused.StatusCode);
        Assert.Contains(refused.Problems!, problem => problem.StartsWith("Chain Block") && problem.Contains("not filled in"));
        var after = await Run(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector"));
        Assert.Equal(JobStatus.InProgress, after.RequestStatus);
        Assert.Equal(MachineStatus.Draft, after.Machines.Single(machine => machine.RequestItemId == forkliftItemId).Status); // nothing was submitted

        // An incomplete certificate is named too, with what it lacks.
        await Run(service => service.SaveFormAsync(requestId, chainItemId, inspector.Id, "inspector", new SaveMachineFormRequest { Texts = new Dictionary<string, string> { ["testType"] = "Visual" } }));
        var incomplete = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.SubmitAllAsync(requestId, inspector.Id, "inspector")));
        Assert.Contains(incomplete.Problems!, problem => problem.StartsWith("Chain Block") && problem.Contains("result"));
    }

    [Fact]
    public async Task CertificatesAlreadySubmittedAreLeftAloneAndOnlyTheJobsInspectorSubmitsEverything()
    {
        await Start();
        await Run(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector", CompleteForm("forklift")));
        await Run(service => service.SubmitAsync(requestId, forkliftItemId, inspector.Id, "inspector"));
        await Run(service => service.SaveFormAsync(requestId, chainItemId, inspector.Id, "inspector", CompleteForm("basic")));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Run(service => service.SubmitAllAsync(requestId, otherInspector.Id, "inspector")));
        var denied = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.SubmitAllAsync(requestId, coordinator.Id, "coordinator")));
        Assert.Equal(403, denied.StatusCode);

        var list = await Run(service => service.SubmitAllAsync(requestId, inspector.Id, "inspector"));
        Assert.Equal(JobStatus.CertificateSubmitted, list.RequestStatus);

        var again = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.SubmitAllAsync(requestId, inspector.Id, "inspector")));
        Assert.Equal("inspection_not_started", again.Code);
    }

    // ---- The certificate answers: defaults and opposite pairs ---------------------------------------------------------

    [Fact]
    public async Task ACertificateStartsWithYesEverywhereExceptTheTwelveMonthsAndTheOppositesOfThePairs()
    {
        await Start();

        var form = await Run(service => service.GetFormAsync(requestId, forkliftItemId, inspector.Id, "inspector"));

        Assert.True(form.Answers["within6Months"]);
        Assert.False(form.Answers["within12Months"]);
        Assert.False(form.Answers["firstExamination"]);
        Assert.True(form.Answers["installedCorrectly"]); // the opposite of the first examination
        Assert.True(form.Answers["examinationScheme"]);
        Assert.True(form.Answers["exceptionalCircumstances"]);
        Assert.False(form.Answers["existingDanger"]); // Yes here would mean a defect exists
        Assert.False(form.Answers["futureDanger"]);
    }

    [Fact]
    public async Task FirstExaminationAndInstalledCorrectlyAreAlwaysOppositeAndSoAreTheTwoIntervals()
    {
        await Start();
        async Task<IReadOnlyDictionary<string, bool>> Save(Dictionary<string, bool> answers) =>
            (await Run(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector", new SaveMachineFormRequest { Answers = answers }))).Answers;

        var first = await Save(new() { ["firstExamination"] = true });
        Assert.False(first["installedCorrectly"]);
        var installed = await Save(new() { ["installedCorrectly"] = true });
        Assert.False(installed["firstExamination"]);
        var six = await Save(new() { ["within6Months"] = false });
        Assert.True(six["within12Months"]);
        var twelve = await Save(new() { ["within12Months"] = false });
        Assert.True(twelve["within6Months"]);

        await Assert.ThrowsAsync<ArgumentException>(() => Save(new() { ["firstExamination"] = true, ["installedCorrectly"] = true }));
        await Assert.ThrowsAsync<ArgumentException>(() => Save(new() { ["within6Months"] = true, ["within12Months"] = true }));
        var both = await Save(new() { ["firstExamination"] = false, ["installedCorrectly"] = true });
        Assert.True(both["installedCorrectly"]);
    }

    // ---- The name fixes the standard and the kind -------------------------------------------------------------------

    [Fact]
    public async Task TheNameDecidesTheKindAndTheStandardOfInspection()
    {
        Assert.Equal(58, CertificateTemplateCatalog.Items.Count);
        Assert.Equal(17, CertificateTemplateCatalog.Items.Count(item => item.Kind == CatalogKinds.Gear));
        Assert.Equal(23, CertificateTemplateCatalog.Items.Count(item => item.Kind == CatalogKinds.Equipment));
        Assert.Equal(18, CertificateTemplateCatalog.Items.Count(item => item.Kind == CatalogKinds.NonLifting));
        Assert.Equal(CatalogKinds.NonLifting, CertificateTemplateCatalog.Resolve("Air Compressor").Item!.Kind);
        Assert.Equal(CatalogKinds.Equipment, CertificateTemplateCatalog.Resolve("Forklift Truck").Item!.Kind);
        Assert.Equal(["BS EN 361:2002"], CertificateTemplateCatalog.Resolve("Full Body Safety Harness").Item!.Standards);
        // Only three names have no standard yet.
        Assert.Equal(["Container", "Ship to Shore Container crane (STS)", "Trailer Truck"],
            CertificateTemplateCatalog.Items.Where(item => item.Standards.Count == 0).Select(item => item.Name).Order().ToArray());

        await Start();
        var list = await Run(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector"));
        var forklift = list.Machines.Single(machine => machine.RequestItemId == forkliftItemId);
        Assert.Equal(CatalogKinds.Equipment, forklift.Kind);
        Assert.Equal(CertificateTemplateCatalog.Resolve("Forklift Truck").Item!.Standards, forklift.InspectionStandards);
        var types = new DirectCertificationService(null!, null!, null!, null!).MachineTypes();
        Assert.Equal(CatalogKinds.Gear, types.Single(type => type.Name == "Eye Bolt").Kind);
        Assert.All(types, type => Assert.Contains(type.Kind, new[] { CatalogKinds.Gear, CatalogKinds.Equipment, CatalogKinds.NonLifting }));
    }

    // ---- Gears: quantity is for gears only -------------------------------------------------------------------------

    [Fact]
    public void QuantityIsKeptForGearsAndIsOneForEveryOtherMachine()
    {
        Assert.Equal(40, GearRules.QuantityFor("Chain Sling grade 4", 40));
        Assert.Equal(0, GearRules.QuantityFor("Chain Sling grade 4", 0)); // the item refuses it
        Assert.Equal(1, GearRules.QuantityFor("Forklift Truck", 5));
        Assert.Equal(1, GearRules.QuantityFor("A machine the catalogue does not know", 3));
        Assert.Equal(1, GearRules.QuantityFor(null, 9));
    }

    [Fact]
    public async Task AMachineAddedToAJobIsOneUnitUnlessItIsAGear()
    {
        await Start();

        var list = await Machines(service => service.AddAsync(requestId, inspector.Id, "inspector", new AddMachineRequest { Name = "Scissor Lift", Quantity = 6 }));
        var gears = await Machines(service => service.AddAsync(requestId, inspector.Id, "inspector", new AddMachineRequest { Name = "Chain Sling grade 4", Quantity = 6 }));

        Assert.Equal(1, list.Machines.Single(machine => machine.Name == "Scissor Lift").Quantity);
        Assert.Equal(6, gears.Machines.Single(machine => machine.Name == "Chain Sling grade 4").Quantity);
    }

    // ---- Gears: the inspector divides a gear into certificates, each with its own qty and serial text ----------------

    private async Task<Guid> AddSlings(int quantity = 1000)
    {
        await Start();
        var list = await Machines(service => service.AddAsync(requestId, inspector.Id, "inspector",
            new AddMachineRequest { Name = "Chain Sling grade 4", Quantity = quantity }));
        return list.Machines.Single(machine => machine.Name == "Chain Sling grade 4").RequestItemId;
    }

    private static DivideMachineRequest Division(params (int Quantity, string Serials)[] shares) =>
        new() { Certificates = shares.Select(share => new CertificateShare { Quantity = share.Quantity, SerialNumbers = share.Serials }).ToArray() };

    [Fact]
    public async Task AGearIsDividedIntoCertificatesWithTheirOwnQtyAndSerials()
    {
        var slings = await AddSlings();

        var list = await Machines(service => service.DivideAsync(requestId, slings, inspector.Id, "inspector",
            Division((150, "CS-0001-CS-0150"), (150, "CS-0151-CS-0300"), (700, "CS-0301-CS-1000, X-9"))));

        var parts = list.Machines.Where(machine => machine.Name == "Chain Sling grade 4").OrderBy(machine => machine.Quantity).ToList();
        Assert.Equal([150, 150, 700], parts.Select(machine => machine.Quantity).ToArray());
        Assert.Contains(parts, machine => machine.RequestItemId == slings); // the machine keeps the first share
        Assert.Equal("CS-0301-CS-1000, X-9", parts.Single(machine => machine.Quantity == 700).SerialNumber);
        Assert.Single(parts.Select(machine => machine.DivisionGroupId).Distinct());
        Assert.NotNull(parts[0].DivisionGroupId);
        Assert.All(parts, machine => Assert.Equal(1000, machine.DivisionTotal));
        Assert.All(parts, machine => Assert.True(machine.IsGear));
        Assert.Equal(5, list.Machines.Count); // the forklift, the chain block and the three certificates of the slings

        // Each share is its own certificate, filled and submitted like any machine.
        foreach (var part in parts)
        {
            await Run(service => service.SaveFormAsync(requestId, part.RequestItemId, inspector.Id, "inspector", CompleteForm("basic")));
            await Run(service => service.SubmitAsync(requestId, part.RequestItemId, inspector.Id, "inspector"));
        }
    }

    [Fact]
    public async Task DividingAgainReplacesTheFirstDivisionAndAnyTotalIsAccepted()
    {
        var slings = await AddSlings();
        await Machines(service => service.DivideAsync(requestId, slings, inspector.Id, "inspector", Division((300, "A-1-A-300"), (300, "A-301-A-600"), (400, "A-601-A-1000"))));

        var again = await Machines(service => service.DivideAsync(requestId, slings, inspector.Id, "inspector", Division((250, "B-1-B-250"), (250, "B-251-B-500"))));

        var parts = again.Machines.Where(machine => machine.Name == "Chain Sling grade 4").ToList();
        Assert.Equal(2, parts.Count);
        Assert.Equal(500, parts.Sum(machine => machine.Quantity)); // a total that differs from 1000 is not refused
        Assert.All(parts, machine => Assert.Equal(1000, machine.DivisionTotal)); // the total of the first division is kept
        Assert.DoesNotContain(parts, machine => machine.SerialNumber!.StartsWith("A-"));
        await using var db = CreateContext();
        Assert.Equal(2, db.Requests.Include(value => value.Items).Single(value => value.Id == requestId).Items.Count(item => item.Name == "Chain Sling grade 4"));
    }

    [Fact]
    public async Task ThereIsNoLimitOnTheNumberOfCertificates()
    {
        var slings = await AddSlings(120);

        var list = await Machines(service => service.DivideAsync(requestId, slings, inspector.Id, "inspector",
            Division(Enumerable.Range(1, 60).Select(number => (2, $"S-{number}")).ToArray())));

        Assert.Equal(60, list.Machines.Count(machine => machine.Name == "Chain Sling grade 4"));
    }

    [Fact]
    public async Task TheSerialTextIsLimitedByItsCharactersOnly()
    {
        var slings = await AddSlings();
        var exactly = new string('7', SerialNumbers.MaxCharacters);

        await Machines(service => service.DivideAsync(requestId, slings, inspector.Id, "inspector", Division((10, exactly))));
        var tooLong = await Assert.ThrowsAsync<ArgumentException>(() => Machines(service => service.DivideAsync(
            requestId, slings, inspector.Id, "inspector", Division((10, "S-1"), (10, exactly + "7")))));
        Assert.Contains("Certificate 2", tooLong.Message);
        Assert.Contains("1001", tooLong.Message);

        await Assert.ThrowsAsync<ArgumentException>(() => Machines(service => service.DivideAsync(requestId, slings, inspector.Id, "inspector", Division((0, "S-1")))));
        await Assert.ThrowsAsync<ArgumentException>(() => Machines(service => service.DivideAsync(requestId, slings, inspector.Id, "inspector", Division((5, "  ")))));
        await Assert.ThrowsAsync<ArgumentException>(() => Machines(service => service.DivideAsync(requestId, slings, inspector.Id, "inspector", Division())));

        // Free text: ranges, commas, repeats and counts are not checked.
        await Machines(service => service.DivideAsync(requestId, slings, inspector.Id, "inspector", Division((4, "500-600, 500-600"), (900, "700"))));
    }

    [Fact]
    public async Task AGearCertificateSerialTextAndFreeTextsShareTheOnePageBudget()
    {
        var slings = await AddSlings();
        await Machines(service => service.DivideAsync(requestId, slings, inspector.Id, "inspector", Division((10, new string('7', SerialNumbers.MaxCharacters)))));

        // 1000 serial characters leave 200 for the four free texts; 201 does not fit on one page.
        var within = CompleteForm("basic") with { Texts = new Dictionary<string, string> { ["testType"] = "Visual", ["defectDescription"] = new string('d', 200) } };
        await Run(service => service.SaveFormAsync(requestId, slings, inspector.Id, "inspector", within));
        var over = CompleteForm("basic") with { Texts = new Dictionary<string, string> { ["defectDescription"] = new string('d', 201) } };
        var refused = await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.SaveFormAsync(requestId, slings, inspector.Id, "inspector", over)));
        Assert.Contains("one page holds 1200", refused.Message);

        // A machine that is not a gear is not held to the budget.
        await Run(service => service.SaveFormAsync(requestId, forkliftItemId, inspector.Id, "inspector",
            CompleteForm("forklift") with { Texts = new Dictionary<string, string> { ["testType"] = "Visual", ["defectDescription"] = new string('d', 1500) } }));
    }

    [Fact]
    public async Task OnlyAGearIsDividedAndNotOnceACertificateOfItStarted()
    {
        await Start();
        var notAGear = await Assert.ThrowsAsync<AuthException>(() => Machines(service => service.DivideAsync(
            requestId, forkliftItemId, inspector.Id, "inspector", Division((1, "FK-1")))));
        Assert.Equal("not_a_gear", notAGear.Code);

        var slings = await AddSlings();
        var list = await Machines(service => service.DivideAsync(requestId, slings, inspector.Id, "inspector", Division((500, "S-1-S-500"), (500, "S-501-S-1000"))));
        var second = list.Machines.Where(machine => machine.Name == "Chain Sling grade 4").Select(machine => machine.RequestItemId).First(id => id != slings);
        await Run(service => service.SaveFormAsync(requestId, second, inspector.Id, "inspector", CompleteForm("basic")));

        var started = await Assert.ThrowsAsync<AuthException>(() => Machines(service => service.DivideAsync(
            requestId, slings, inspector.Id, "inspector", Division((1000, "S-1-S-1000")))));
        Assert.Equal("machine_already_started", started.Code);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Machines(service => service.DivideAsync(
            requestId, slings, otherInspector.Id, "inspector", Division((1, "S-1")))));
    }

    // ---- A registered gear keeps no serial number ------------------------------------------------------------------

    [Fact]
    public async Task ARegisteredItemThatIsAGearKeepsNoSerialNumberButOtherItemsDo()
    {
        await using var db = CreateContext();
        var items = new EquipmentService(new EfEquipmentRepository(db));

        var sling = await items.CreateAsync(new SaveEquipmentRequest { ClientId = clientId, Name = "Chain Sling grade 4", SerialNumber = "CS-0001" });
        var lift = await items.CreateAsync(new SaveEquipmentRequest { ClientId = clientId, Name = "Scissor Lift", SerialNumber = "SL-9" });
        var changed = await items.UpdateAsync(sling.Id, new SaveEquipmentRequest { ClientId = clientId, Name = "Chain Sling grade 4", SerialNumber = "CS-0002", Capacity = "2 t" });

        Assert.Null(sling.SerialNumber);
        Assert.Equal("SL-9", lift.SerialNumber);
        Assert.Null(changed.SerialNumber);
        Assert.Equal("2 t", changed.Capacity);
        Assert.Null(db.Items.Single(item => item.Id == sling.Id).SerialNumber); // nothing was stored
        Assert.Equal("SL-9", db.Items.Single(item => item.Id == lift.Id).SerialNumber);
        Assert.Null(GearRules.SerialFor("Eye Bolt", "X-1"));
        Assert.Equal("X-1", GearRules.SerialFor("Forklift Truck", "X-1"));
    }

    // ---- Gears: one machine, many serial numbers, shared over several certificates ----------------------------------

    private static IReadOnlyList<string> Serials(int count, string prefix = "CS-26-") =>
        Enumerable.Range(1, count).Select(number => prefix + number.ToString("0000")).ToArray();

    [Fact]
    public async Task ADivisionSharesTheSerialNumbersOverCertificatesInOrder()
    {
        await Start();

        var list = await Run(service => service.SplitAsync(requestId, chainItemId, inspector.Id, "inspector",
            new SplitMachineRequest { SerialNumbers = Serials(10), Certificates = 3 }));

        var chains = list.Machines.Where(machine => machine.Name == "Chain Block").OrderBy(machine => machine.SerialNumber).ToList();
        Assert.Equal(3, chains.Count);
        Assert.Equal([4, 3, 3], chains.Select(machine => machine.Quantity).ToArray());
        Assert.Equal("CS-26-0001, CS-26-0002, CS-26-0003, CS-26-0004", chains[0].SerialNumber);
        Assert.Equal("CS-26-0010", chains[2].SerialNumber!.Split(", ").Last());
        Assert.Equal(10, chains.SelectMany(machine => machine.SerialNumber!.Split(", ")).Distinct().Count());
        Assert.Contains(list.Machines, machine => machine.Name == "Forklift Truck");
        Assert.Equal(4, list.Progress.Total);

        // Every share is its own certificate: it can be filled and submitted like any machine.
        var second = chains[1].RequestItemId;
        await Run(service => service.SaveFormAsync(requestId, second, inspector.Id, "inspector", CompleteForm("basic")));
        Assert.Equal(MachineStatus.Draft, (await Run(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector")))
            .Machines.Single(machine => machine.RequestItemId == second).Status);
    }

    [Fact]
    public async Task WithoutACountTheFewestCertificatesThatFitAreMade()
    {
        await Start();
        var limit = SerialNumbers.PerCertificate(SerialNumbers.MaxLength);
        var serials = Serials(1000);

        var list = await Run(service => service.SplitAsync(requestId, chainItemId, inspector.Id, "inspector",
            new SplitMachineRequest { SerialNumbers = serials }));

        var chains = list.Machines.Where(machine => machine.Name == "Chain Block").ToList();
        var expected = (int)Math.Ceiling(1000 / (double)SerialNumbers.PerCertificate(serials.Max(value => value.Length)));
        Assert.Equal(expected, chains.Count);
        Assert.True(expected < 1000 / limit * 3);
        Assert.All(chains, machine => Assert.True(machine.SerialNumber!.Length <= SerialNumbers.MaxCharacters));
        Assert.Equal(1000, chains.Sum(machine => machine.Quantity));
    }

    [Fact]
    public async Task ADivisionThatWouldOverflowACertificateIsRefused()
    {
        await Start();

        var refused = await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.SplitAsync(
            requestId, chainItemId, inspector.Id, "inspector", new SplitMachineRequest { SerialNumbers = Serials(200), Certificates = 2 })));
        Assert.Contains("Use at least", refused.Message);

        await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.SplitAsync(
            requestId, chainItemId, inspector.Id, "inspector", new SplitMachineRequest { SerialNumbers = ["A-1", "a-1"] })));
        await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.SplitAsync(
            requestId, chainItemId, inspector.Id, "inspector", new SplitMachineRequest { SerialNumbers = ["A-1", "B,2"] })));
        await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.SplitAsync(
            requestId, chainItemId, inspector.Id, "inspector", new SplitMachineRequest { SerialNumbers = [" ", ""] })));
        Assert.Equal(2, (await Run(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector"))).Machines.Count);
    }

    [Fact]
    public async Task AMachineIsDividedOnlyBeforeItsCertificateStartsAndByItsInspector()
    {
        var early = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.SplitAsync(
            requestId, chainItemId, inspector.Id, "inspector", new SplitMachineRequest { SerialNumbers = Serials(4) })));
        Assert.Equal("inspection_not_started", early.Code);

        await Start();
        var stranger = await Assert.ThrowsAsync<KeyNotFoundException>(() => Run(service => service.SplitAsync(
            requestId, chainItemId, otherInspector.Id, "inspector", new SplitMachineRequest { SerialNumbers = Serials(4) })));
        Assert.NotNull(stranger);

        await Run(service => service.SaveFormAsync(requestId, chainItemId, inspector.Id, "inspector", CompleteForm("basic")));
        var started = await Assert.ThrowsAsync<AuthException>(() => Run(service => service.SplitAsync(
            requestId, chainItemId, inspector.Id, "inspector", new SplitMachineRequest { SerialNumbers = Serials(4) })));
        Assert.Equal("machine_already_started", started.Code);
    }

    [Fact]
    public async Task ASerialNumberListLongerThanOneCertificateTakesIsRefusedOnSave()
    {
        await Start();
        var form = CompleteForm("basic") with { Fields = new Dictionary<string, string> { ["serialNumber"] = string.Join(", ", Serials(200)) } };

        var refused = await Assert.ThrowsAsync<ArgumentException>(() => Run(service => service.SaveFormAsync(
            requestId, chainItemId, inspector.Id, "inspector", form)));
        Assert.Contains("Divide the machine", refused.Message);
    }

    [Fact]
    public async Task GearsAreNotRegisteredAsItemsButOtherMachinesAre()
    {
        Assert.Equal(17, CertificateTemplateCatalog.Items.Count(item => item.Gear));
        Assert.True(CertificateTemplateCatalog.Resolve("Chain Sling grade 4").Item!.Gear);
        Assert.False(CertificateTemplateCatalog.Resolve("Forklift Truck").Item!.Gear);

        await using var db = CreateContext();
        var request = InspectionRequest.Create("Al Noor Industries", "Cairo", clientId,
            [RequestItem.Create("Chain Sling grade 4", 40, serialNumber: "S-1"), RequestItem.Create("Scissor Lift", 1, serialNumber: "SL-9")],
            coordinator.Id, managerId: manager.Id);
        await new RequestEquipmentRegistrar(new EfEquipmentCatalog(db)).RegisterAsync(request);

        Assert.Null(request.Items.Single(item => item.Name == "Chain Sling grade 4").EquipmentId);
        Assert.NotNull(request.Items.Single(item => item.Name == "Scissor Lift").EquipmentId);
    }

    [Fact]
    public async Task AGearCertificateWithManySerialNumbersIsDrawnAsOnePdf()
    {
        await Start();
        var list = await Run(service => service.SplitAsync(requestId, chainItemId, inspector.Id, "inspector",
            new SplitMachineRequest { SerialNumbers = Serials(1000) }));
        var first = list.Machines.Where(machine => machine.Name == "Chain Block").OrderByDescending(machine => machine.SerialNumber!.Length).First();
        Assert.True(first.SerialNumber!.Length > 400);
        await Run(service => service.SaveFormAsync(requestId, first.RequestItemId, inspector.Id, "inspector", CompleteForm("basic")));

        var pdf = await Run(service => service.GetPdfAsync(requestId, first.RequestItemId, inspector.Id, "inspector"));

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf.Content, 0, 4));
    }

    // ---- A client with a contract: the request goes straight to an inspector --------------------------------------------

    private async Task<Guid> NewRequestFor(bool contract)
    {
        Guid clientForRequest;
        await using (var db = CreateContext())
            clientForRequest = (await new ClientService(new EfClientRepository(db), new EfUserRepository(db)).CreateAsync(new CreateClientRequest
            {
                Name = contract ? "Contract Client Co." : "Walk-in Client Co.", HasContract = contract, Location = "Alexandria",
                CertificatesEmail = "certificates@client.com"
            }, manager.Id)).Id;
        await using var save = CreateContext();
        var request = InspectionRequest.Create(contract ? "Contract Client Co." : "Walk-in Client Co.", "Alexandria", clientForRequest,
            [RequestItem.Create("Air Compressor", 1)], coordinator.Id, managerId: manager.Id);
        request.UseReviewer(manager.Id);
        save.Requests.Add(request);
        await save.SaveChangesAsync();
        return request.Id;
    }

    private async Task<T> Requests<T>(Func<InspectionRequestService, Task<T>> action)
    {
        await using var db = CreateContext();
        return await action(new InspectionRequestService(
            new EfRequestRepository(db), new EfUserRepository(db), clientRepository: new EfClientRepository(db),
            quoteRepository: new EfQuoteRepository(db), transactions: new EfTransactionRunner(db)));
    }

    private static Inspecta.Application.Contracts.Requests.AssignWithoutQuotationRequest Assignment(Guid inspectorId) =>
        new() { InspectorId = inspectorId, ScheduledAt = DateTimeOffset.UtcNow.AddDays(1), Note = "Contract visit." };

    [Fact]
    public async Task ARequestOfAContractClientGoesStraightToAnInspectorWithoutAQuotation()
    {
        var contractRequest = await NewRequestFor(contract: true);
        var plainRequest = await NewRequestFor(contract: false);
        Assert.True((await Requests(service => service.GetVisibleAsync(contractRequest, coordinator.Id, "coordinator")))!.CanAssignWithoutQuotation);
        Assert.False((await Requests(service => service.GetVisibleAsync(plainRequest, coordinator.Id, "coordinator")))!.CanAssignWithoutQuotation);

        var assigned = await Requests(service => service.AssignWithoutQuotationAsync(contractRequest, coordinator.Id, "coordinator", Assignment(inspector.Id)));

        Assert.Equal(JobStatus.Assigned, assigned.Status);
        Assert.Equal(inspector.Id, assigned.InspectorId);
        Assert.Equal(Inspecta.Domain.Enums.RequestKind.Contract, assigned.Kind);
        Assert.False(assigned.CanAssignWithoutQuotation);
        await using (var db = CreateContext())
        {
            // A placeholder accepted quotation puts the job in the quotation queue.
            var quote = db.Quotes.Single(value => value.RequestId == contractRequest);
            Assert.Equal(QuoteStatus.Accepted, quote.Status);
            Assert.StartsWith("CT-", quote.QuotationNumber);
        }

        // The inspector starts it and sees the machines of the request.
        var started = await Run(service => service.StartInspectionAsync(contractRequest, inspector.Id, "inspector"));
        Assert.Equal(JobStatus.InProgress, started.RequestStatus);
        Assert.Equal("Air Compressor", started.Machines.Single().Name);
    }

    [Fact]
    public async Task OnlyAContractClientsNewRequestCanGoWithoutAQuotationAndOnlyToATeamInspector()
    {
        var plainRequest = await NewRequestFor(contract: false);
        var noContract = await Assert.ThrowsAsync<AuthException>(() => Requests(service =>
            service.AssignWithoutQuotationAsync(plainRequest, coordinator.Id, "coordinator", Assignment(inspector.Id))));
        Assert.Equal("no_contract", noContract.Code);

        var contractRequest = await NewRequestFor(contract: true);
        var byInspector = await Assert.ThrowsAsync<AuthException>(() => Requests(service =>
            service.AssignWithoutQuotationAsync(contractRequest, inspector.Id, "inspector", Assignment(inspector.Id))));
        Assert.Equal(403, byInspector.StatusCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Requests(service =>
            service.AssignWithoutQuotationAsync(contractRequest, coordinator.Id, "coordinator", Assignment(coordinator.Id))));

        await Requests(service => service.AssignWithoutQuotationAsync(contractRequest, coordinator.Id, "coordinator", Assignment(inspector.Id)));
        var again = await Assert.ThrowsAsync<AuthException>(() => Requests(service =>
            service.AssignWithoutQuotationAsync(contractRequest, coordinator.Id, "coordinator", Assignment(otherInspector.Id))));
        Assert.Equal("not_assignable", again.Code);
    }

    // ---- Man day, and machines added to a job on site ---------------------------------------------------------------

    private async Task<T> Machines<T>(Func<JobMachineService, Task<T>> action)
    {
        await using var db = CreateContext();
        var inspection = new MachineInspectionService(
            new EfCertificateRepository(db), new EfRequestRepository(db), new EfUserRepository(db), new EfEquipmentRepository(db),
            new EfClientRepository(db), pdfServices.GetRequiredService<IPdfGenerator>(), new EfCertificateFileRepository(db),
            qrCodes: new Inspecta.Infrastructure.Services.QrCodeRenderer(), transactions: new EfTransactionRunner(db));
        var catalog = new EfEquipmentCatalog(db);
        return await action(new JobMachineService(
            new EfRequestRepository(db), new EfCertificateRepository(db), catalog, new RequestEquipmentRegistrar(catalog),
            new EquipmentService(new EfEquipmentRepository(db)), inspection));
    }

    private async Task<Inspecta.Application.Contracts.Requests.RequestResponse> RequestManDay(Guid inspectorId, string role = "coordinator", Guid? actor = null)
    {
        await using var db = CreateContext();
        var service = new InspectionRequestService(
            new EfRequestRepository(db), new EfUserRepository(db), clientRepository: new EfClientRepository(db),
            quoteRepository: new EfQuoteRepository(db), transactions: new EfTransactionRunner(db),
            reviewers: new ReviewerService(new EfUserRepository(db), new FakeCompanyRepository(TestCompanies.HeadedBy(manager)), new EfRequestRepository(db)));
        return await service.CreateManDayAsync(
            new Inspecta.Application.Contracts.Requests.CreateManDayRequest { ClientId = clientId, InspectorId = inspectorId, ScheduledAt = DateTimeOffset.UtcNow.AddDays(1), Note = "Check the new workshop." },
            actor ?? coordinator.Id, role, manager.Id);
    }

    [Fact]
    public async Task AManDayGoesStraightToTheInspectorWhoAddsAndCertifiesTheMachinesFoundOnSite()
    {
        var manDay = await RequestManDay(inspector.Id);

        Assert.Equal(Inspecta.Domain.Enums.RequestKind.ManDay, manDay.Kind);
        Assert.Equal(JobStatus.Assigned, manDay.Status);
        Assert.Equal(inspector.Id, manDay.InspectorId);
        Assert.Empty(manDay.Items);
        Assert.Equal("Cairo, Industrial City", manDay.Location);
        await using (var db = CreateContext())
        {
            // A placeholder accepted quotation, so the job shows in the quotation queue.
            var quote = db.Quotes.Single(value => value.RequestId == manDay.Id);
            Assert.Equal(QuoteStatus.Accepted, quote.Status);
            Assert.StartsWith("MD-", quote.QuotationNumber);
            Assert.Equal(0, quote.AmountPiastres);
        }

        var started = await Run(service => service.StartInspectionAsync(manDay.Id, inspector.Id, "inspector"));
        Assert.Equal(JobStatus.InProgress, started.RequestStatus);
        Assert.Empty(started.Machines);

        // The registered forklift is on offer; a machine found on site is registered for the client at once.
        var offered = await Machines(service => service.AvailableAsync(manDay.Id, inspector.Id, "inspector"));
        Assert.Equal("Forklift Truck", offered.Single().Name);
        var withForklift = await Machines(service => service.AddAsync(manDay.Id, inspector.Id, "inspector", new AddMachineRequest { EquipmentId = forkliftEquipmentId }));
        var list = await Machines(service => service.AddAsync(manDay.Id, inspector.Id, "inspector",
            new AddMachineRequest { Name = "Scissor Lift", SerialNumber = "SL-77", Capacity = "500 kg" }));
        await Machines(service => service.AddAsync(manDay.Id, inspector.Id, "inspector", new AddMachineRequest { Name = "Chain Sling grade 4", Quantity = 12 }));
        Assert.Single(withForklift.Machines);
        Assert.Equal(2, list.Machines.Count);

        await using (var db = CreateContext())
        {
            var registered = db.Items.Where(item => item.ClientId == clientId).Select(item => item.Name).ToList();
            Assert.Contains("Scissor Lift", registered);
            Assert.DoesNotContain("Chain Sling grade 4", registered);
            Assert.NotNull(db.Requests.Include(value => value.Items).Single(value => value.Id == manDay.Id).Items.Single(item => item.Name == "Scissor Lift").EquipmentId);
        }

        Assert.Empty(await Machines(service => service.AvailableAsync(manDay.Id, inspector.Id, "inspector")));
        var all = await Run(service => service.ListMachinesAsync(manDay.Id, inspector.Id, "inspector"));
        Assert.Equal(3, all.Machines.Count);
        foreach (var machine in all.Machines)
        {
            var key = machine.TemplateKey;
            await Run(service => service.SaveFormAsync(manDay.Id, machine.RequestItemId, inspector.Id, "inspector", CompleteForm(key)));
            await Run(service => service.SubmitAsync(manDay.Id, machine.RequestItemId, inspector.Id, "inspector"));
        }

        var review = await Run(service => service.ListMachinesAsync(manDay.Id, manager.Id, "technical_manager"));
        Assert.Equal(JobStatus.CertificateSubmitted, review.RequestStatus);
        Assert.True(review.CanApprove);
    }

    [Fact]
    public async Task OnlyThePeopleOfTheJobChangeItsMachinesAndOnlyUntilItIsSubmitted()
    {
        await Start();
        var item = new AddMachineRequest { Name = "Scissor Lift", SerialNumber = "SL-1" };

        // The coordinator and the technical manager can; an inspector without the job cannot even see it.
        var byCoordinator = await Machines(service => service.AddAsync(requestId, coordinator.Id, "coordinator", item));
        Assert.Equal(3, byCoordinator.Machines.Count);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Machines(service => service.AddAsync(requestId, otherInspector.Id, "inspector", item)));
        await Machines(service => service.AddAsync(requestId, manager.Id, "technical_manager", new AddMachineRequest { Name = "Hydraulic Jack" }));

        // A registered machine already in the job, or one of another client, is refused.
        await Assert.ThrowsAsync<InvalidOperationException>(() => Machines(service => service.AddAsync(
            requestId, inspector.Id, "inspector", new AddMachineRequest { EquipmentId = forkliftEquipmentId })));
        await Assert.ThrowsAsync<ArgumentException>(() => Machines(service => service.AddAsync(
            requestId, inspector.Id, "inspector", new AddMachineRequest { EquipmentId = Guid.NewGuid() })));
        await Assert.ThrowsAsync<ArgumentException>(() => Machines(service => service.AddAsync(requestId, inspector.Id, "inspector", new AddMachineRequest())));

        // Edit and remove: before the certificate starts both work; after it starts the machine stays; once submitted it is locked.
        var scissor = byCoordinator.Machines.Single(machine => machine.Name == "Scissor Lift").RequestItemId;
        var edited = await Machines(service => service.EditAsync(requestId, scissor, inspector.Id, "inspector",
            new EditMachineRequest { Name = "Scissor Lift", Quantity = 2, SerialNumber = "SL-1, SL-2", Capacity = "300 kg" }));
        var changed = edited.Machines.Single(machine => machine.RequestItemId == scissor);
        Assert.Equal("SL-1, SL-2", changed.SerialNumber);
        Assert.Equal(1, changed.Quantity); // not a gear: one unit
        await Run(service => service.SaveFormAsync(requestId, scissor, inspector.Id, "inspector", CompleteForm("basic")));
        var started = await Assert.ThrowsAsync<AuthException>(() => Machines(service => service.RemoveAsync(requestId, scissor, inspector.Id, "inspector")));
        Assert.Equal("machine_already_started", started.Code);
        await Run(service => service.SubmitAsync(requestId, scissor, inspector.Id, "inspector"));
        var locked = await Assert.ThrowsAsync<AuthException>(() => Machines(service => service.EditAsync(requestId, scissor, inspector.Id, "inspector",
            new EditMachineRequest { Name = "Scissor Lift", Quantity = 3 })));
        Assert.Equal("certificate_locked", locked.Code);

        var jack = (await Run(service => service.ListMachinesAsync(requestId, inspector.Id, "inspector"))).Machines.Single(machine => machine.Name == "Hydraulic Jack").RequestItemId;
        var removed = await Machines(service => service.RemoveAsync(requestId, jack, coordinator.Id, "coordinator"));
        Assert.DoesNotContain(removed.Machines, machine => machine.Name == "Hydraulic Jack");
    }

    [Fact]
    public async Task AManDayIsRequestedByTheTeamForItsOwnInspectorsOnly()
    {
        var byInspector = await Assert.ThrowsAsync<AuthException>(() => RequestManDay(inspector.Id, "inspector", inspector.Id));
        Assert.Equal(403, byInspector.StatusCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => RequestManDay(coordinator.Id));

        var otherManager = AppUser.Create("other-manager@example.com", "Other Manager", UserRole.TechnicalManager);
        var stranger = AppUser.Create("outsider@example.com", "Outsider", UserRole.Inspector, managerId: otherManager.Id);
        await using (var db = CreateContext()) { db.Users.AddRange(otherManager, stranger); await db.SaveChangesAsync(); }
        var foreign = await Assert.ThrowsAsync<AuthException>(() => RequestManDay(stranger.Id));
        Assert.Equal(403, foreign.StatusCode);
    }

    // ---- A certificate without a quotation ------------------------------------------------------------------------

    private async Task<T> Direct<T>(Func<DirectCertificationService, Task<T>> action)
    {
        await using var db = CreateContext();
        var inspection = new MachineInspectionService(
            new EfCertificateRepository(db), new EfRequestRepository(db), new EfUserRepository(db), new EfEquipmentRepository(db),
            new EfClientRepository(db), pdfServices.GetRequiredService<IPdfGenerator>(), new EfCertificateFileRepository(db),
            qrCodes: new Inspecta.Infrastructure.Services.QrCodeRenderer(), transactions: new EfTransactionRunner(db));
        var requests = new InspectionRequestService(new EfRequestRepository(db), new EfUserRepository(db), new RequestEquipmentRegistrar(new EfEquipmentCatalog(db)),
            reviewers: new ReviewerService(new EfUserRepository(db), new FakeCompanyRepository(TestCompanies.HeadedBy(manager)), new EfRequestRepository(db)));
        return await action(new DirectCertificationService(
            requests, new EfRequestRepository(db), new EfClientRepository(db), inspection, new RequestEquipmentRegistrar(new EfEquipmentCatalog(db)),
            new EquipmentService(new EfEquipmentRepository(db)), new EfQuoteRepository(db), new EfTransactionRunner(db)));
    }

    [Fact]
    public async Task ACertificateCreatedDirectlyStartsInProgressForTheInspectorAndReachesReview()
    {
        var job = await Direct(service => service.CreateAsync(new CreateCertificationRequest
        {
            ClientId = clientId,
            Machines =
            [
                new Inspecta.Application.Contracts.Requests.CreateRequestItem { Name = "Forklift Truck", EquipmentId = forkliftEquipmentId, Capacity = "3 ton" },
                new Inspecta.Application.Contracts.Requests.CreateRequestItem { Name = "Scissor Lift", SerialNumber = "SL-9" },
                new Inspecta.Application.Contracts.Requests.CreateRequestItem { Name = "Chain Sling grade 4", Quantity = 12 }
            ]
        }, inspector.Id, "inspector", manager.Id));

        Assert.Equal(JobStatus.InProgress, job.RequestStatus);
        Assert.Equal("Al Noor Industries", job.ClientName);
        Assert.Equal("Cairo, Industrial City", job.Location);
        Assert.Equal(3, job.Machines.Count);
        Assert.All(job.Machines, machine => Assert.True(machine.CanEdit));
        Assert.Contains(job.Machines, machine => machine.Name == "Chain Sling grade 4" && machine.IsGear);

        await using (var db = CreateContext())
        {
            var stored = db.Requests.Include(value => value.Items).Include(value => value.History).Single(value => value.Id == job.RequestId);
            Assert.Equal(inspector.Id, stored.InspectorId);
            Assert.Equal(manager.Id, stored.ManagerId);
            Assert.Equal("Direct certificate", stored.Subject);
            Assert.Equal(Inspecta.Domain.Enums.RequestKind.Direct, stored.Kind);
            Assert.StartsWith("DC-", db.Quotes.Single(value => value.RequestId == job.RequestId).QuotationNumber);
            Assert.Equal(JobStatus.InProgress, stored.History.OrderBy(entry => entry.OccurredAt).Last().ToStatus);
            // The new machine is registered for the client; the gear is not.
            Assert.NotNull(stored.Items.Single(item => item.Name == "Scissor Lift").EquipmentId);
            Assert.Null(stored.Items.Single(item => item.Name == "Chain Sling grade 4").EquipmentId);
        }

        foreach (var machine in job.Machines)
        {
            var key = machine.TemplateKey;
            await Run(service => service.SaveFormAsync(job.RequestId, machine.RequestItemId, inspector.Id, "inspector", CompleteForm(key)));
            await Run(service => service.SubmitAsync(job.RequestId, machine.RequestItemId, inspector.Id, "inspector"));
        }

        var review = await Run(service => service.ListMachinesAsync(job.RequestId, manager.Id, "technical_manager"));
        Assert.Equal(JobStatus.CertificateSubmitted, review.RequestStatus);
        Assert.True(review.CanApprove);
    }

    [Fact]
    public async Task ADirectCertificateNeedsAMachineAnAllowedRoleAndAClientOfTheTeam()
    {
        var item = new Inspecta.Application.Contracts.Requests.CreateRequestItem { Name = "Scissor Lift" };
        await Assert.ThrowsAsync<ArgumentException>(() => Direct(service => service.CreateAsync(
            new CreateCertificationRequest { ClientId = clientId }, inspector.Id, "inspector", manager.Id)));
        var denied = await Assert.ThrowsAsync<AuthException>(() => Direct(service => service.CreateAsync(
            new CreateCertificationRequest { ClientId = clientId, Machines = [item] }, coordinator.Id, "coordinator", manager.Id)));
        Assert.Equal(403, denied.StatusCode);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Direct(service => service.CreateAsync(
            new CreateCertificationRequest { ClientId = Guid.NewGuid(), Machines = [item] }, inspector.Id, "inspector", manager.Id)));
    }

    [Fact]
    public async Task AnInspectorCertifiesOnlyTheClientsOfTheJobsInTheirHands()
    {
        var mine = await Direct(service => service.ClientsAsync(inspector.Id, "inspector"));
        Assert.Equal(["Al Noor Industries"], mine.Select(client => client.Name).ToArray());
        Assert.Equal(forkliftEquipmentId, (await Direct(service => service.MachinesOfAsync(clientId, inspector.Id, "inspector"))).Single().Id);

        // Another inspector has no job for this client: it is not listed, its machines are not found, and no certificate can be created for it.
        Assert.Empty(await Direct(service => service.ClientsAsync(otherInspector.Id, "inspector")));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Direct(service => service.MachinesOfAsync(clientId, otherInspector.Id, "inspector")));
        var denied = await Assert.ThrowsAsync<AuthException>(() => Direct(service => service.CreateAsync(new CreateCertificationRequest
        {
            ClientId = clientId,
            Machines = [new Inspecta.Application.Contracts.Requests.CreateRequestItem { Name = "Scissor Lift" }]
        }, otherInspector.Id, "inspector", manager.Id)));
        Assert.Equal(403, denied.StatusCode);

        // The technical manager can certify for any client of the team; a coordinator cannot use it at all.
        Assert.Single(await Direct(service => service.ClientsAsync(manager.Id, "technical_manager")));
        await Assert.ThrowsAsync<AuthException>(() => Direct(service => service.ClientsAsync(coordinator.Id, "coordinator")));
    }

    [Fact]
    public async Task OnceTheJobIsSubmittedTheClientNoLongerCountsAsInTheInspectorsHands()
    {
        await SubmitBothMachines();

        Assert.Empty(await Direct(service => service.ClientsAsync(inspector.Id, "inspector")));
    }

    [Fact]
    public async Task TheCatalogueIsListedWithItsGears()
    {
        await using var db = CreateContext();
        var types = new DirectCertificationService(null!, null!, null!, null!).MachineTypes();

        Assert.Equal(58, types.Count);
        Assert.Equal(17, types.Count(type => type.IsGear));
        Assert.Equal("basic", types.Single(type => type.Name == "Eye Bolt").TemplateKey);
        Assert.False(types.Single(type => type.Name == "Forklift Truck").IsGear);
    }

    private async Task Start() => await Run(service => service.StartInspectionAsync(requestId, inspector.Id, "inspector"));

    private static string FirstRow(string templateKey)
    {
        var template = CertificateTemplateCatalog.Find(templateKey)!;
        return $"{template.Checklist[0].Key}.{template.Checklist[0].Items[0].Key}";
    }

    /// <summary>Every field, row and question answered the way a normal inspection goes.</summary>
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
            Dates = new Dictionary<string, DateOnly>
            {
                ["examination"] = new(2026, 11, 5),
                ["nextExamination"] = new(2027, 11, 5)
            },
            Result = InspectionResult.Satisfactory
        };
    }

    private async Task<T> Run<T>(Func<MachineInspectionService, Task<T>> action)
    {
        await using var db = CreateContext();
        var service = new MachineInspectionService(
            new EfCertificateRepository(db),
            new EfRequestRepository(db),
            new EfUserRepository(db),
            new EfEquipmentRepository(db),
            new EfClientRepository(db),
            pdfServices.GetRequiredService<IPdfGenerator>(),
            new EfCertificateFileRepository(db),
            qrCodes: new Inspecta.Infrastructure.Services.QrCodeRenderer(),
            transactions: new EfTransactionRunner(db));
        return await action(service);
    }

    /// <summary>The real generator, except that it fails on the given call: a PDF that cannot be drawn halfway through an approval.</summary>
    private sealed class FailingOnCall(IPdfGenerator inner, int failOn) : IPdfGenerator
    {
        private int calls;

        public PdfFile Generate<TModel>(TModel model) =>
            ++calls == failOn ? throw new PdfGenerationException("The PDF could not be drawn.") : inner.Generate(model);
    }

    private async Task<T> RunWith<T>(IPdfGenerator generator, Func<MachineInspectionService, Task<T>> action)
    {
        await using var db = CreateContext();
        var service = new MachineInspectionService(
            new EfCertificateRepository(db), new EfRequestRepository(db), new EfUserRepository(db), new EfEquipmentRepository(db),
            new EfClientRepository(db), generator, new EfCertificateFileRepository(db),
            qrCodes: new Inspecta.Infrastructure.Services.QrCodeRenderer(), transactions: new EfTransactionRunner(db));
        return await action(service);
    }

    private async Task SubmitBothMachines()
    {
        await Start();
        foreach (var (item, key) in new[] { (forkliftItemId, "forklift"), (chainItemId, "basic") })
        {
            await Run(service => service.SaveFormAsync(requestId, item, inspector.Id, "inspector", CompleteForm(key)));
            await Run(service => service.SubmitAsync(requestId, item, inspector.Id, "inspector"));
        }
    }

    // A job that reached review without its machines being submitted (the old generic transition could do that).
    private async Task MoveToReviewWithoutCertificates()
    {
        await using var db = CreateContext();
        var request = db.Requests.Include(value => value.Items).Include(value => value.History).Single(value => value.Id == requestId);
        request.TransitionTo(JobStatus.TaskAccepted, inspector.Id);
        request.TransitionTo(JobStatus.InProgress, inspector.Id);
        request.TransitionTo(JobStatus.CertificateSubmitted, inspector.Id);
        await db.SaveChangesAsync();
    }

    private static int PageCount(byte[] pdf)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Count (\d+)");
        return matches.Count == 0 ? -1 : matches.Max(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
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
