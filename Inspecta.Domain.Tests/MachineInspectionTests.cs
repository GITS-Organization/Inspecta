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
        var file = Assert.Single(db.IssuedCertificateFiles.AsNoTracking().ToList());
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

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Run(service => service.ReturnToInspectorAsync(requestId, manager.Id, "technical_manager",
            new ReturnJobRequest { Note = "Fix", Machines = [new MachineReviewComment { RequestItemId = Guid.NewGuid(), Comment = "x" }] })));
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
            qrCodes: new Inspecta.Infrastructure.Services.QrCodeRenderer());
        return await action(service);
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
