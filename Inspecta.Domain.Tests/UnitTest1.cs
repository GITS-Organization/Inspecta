using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

public sealed class RequestLifecycleTests
{
    [Fact]
    public void QuoteCannotBeSentBeforeClientIsMatched()
    {
        var coordinatorId = Guid.NewGuid();
        var request = InspectionRequest.Create(
            "Acme",
            "Cairo",
            null,
            [RequestItem.Create("Crane", 1)],
            coordinatorId);

        request.TransitionTo(JobStatus.QuoteDraft, coordinatorId);

        Assert.Throws<InvalidOperationException>(() => request.TransitionTo(JobStatus.QuoteSent, coordinatorId));
    }

    [Fact]
    public void AcceptedQuoteCanBeAssignedAndHistoryIsRecorded()
    {
        var coordinatorId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var request = InspectionRequest.Create(
            "Acme",
            "Cairo",
            Guid.NewGuid(),
            [RequestItem.Create("Crane", 1)],
            coordinatorId);

        request.TransitionTo(JobStatus.QuoteDraft, coordinatorId);
        request.TransitionTo(JobStatus.QuoteSent, coordinatorId);
        request.TransitionTo(JobStatus.QuoteAccepted, coordinatorId);
        request.AssignInspector(inspectorId, DateTimeOffset.UtcNow.AddDays(1), coordinatorId);

        Assert.Equal(JobStatus.Assigned, request.Status);
        Assert.Equal(inspectorId, request.InspectorId);
        Assert.Equal(JobStatus.Assigned, request.History[^1].ToStatus);
    }

    [Fact]
    public void AssignmentBeforeQuoteAcceptanceIsRejected()
    {
        var request = InspectionRequest.Create(
            "Acme",
            "Cairo",
            Guid.NewGuid(),
            [RequestItem.Create("Crane", 1)],
            Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            request.AssignInspector(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid()));
    }
}

public sealed class CertificateTests
{
    [Fact]
    public void CertificateCannotBeSubmittedUntilRequiredAnswersAreComplete()
    {
        var certificate = InspectionCertificate.Create(Guid.NewGuid(), Guid.NewGuid());
        certificate.SetDraft(
            new Dictionary<string, string>(),
            new Dictionary<string, DateTimeOffset>(),
            new Dictionary<string, bool>(),
            ChecklistAnswer.Unanswered,
            null);

        Assert.Throws<InvalidOperationException>(certificate.Submit);
    }

    [Fact]
    public void CompleteCertificateRequiresNormalizedReviewerSignature()
    {
        var certificate = InspectionCertificate.Create(Guid.NewGuid(), Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        certificate.SetDraft(
            new Dictionary<string, string> { ["standardOfInspection"] = "Standard", ["testType"] = "Visual test" },
            new Dictionary<string, DateTimeOffset> { ["examination"] = now, ["nextExamination"] = now.AddMonths(6) },
            new Dictionary<string, bool>
            {
                ["firstExamination"] = false,
                ["within6Months"] = true,
                ["within12Months"] = false,
                ["examinationScheme"] = true,
                ["exceptionalCircumstances"] = false,
                ["existingDanger"] = false,
                ["futureDanger"] = false
            },
            ChecklistAnswer.Pass,
            CertificateResult.SafeToOperate);
        certificate.Submit();

        Assert.Throws<ArgumentException>(() => certificate.Approve(Guid.NewGuid(), "CERT-001", "VERIFY-001", "client@example.com", "[]"));
        certificate.Approve(Guid.NewGuid(), "CERT-001", "VERIFY-001", "client@example.com", "[[0.1,0.1,0.5,0.5]]");

        Assert.Equal("CERT-001", certificate.CertificateNumber);
    }
}

public sealed class DomainDataModelTests
{
    [Fact]
    public void LegacySupervisorRoleMapsToCoordinator()
    {
        Assert.Equal(UserRole.Coordinator, UserRoleParser.Parse("supervisor"));
        Assert.Equal(UserRole.TechnicalManager, UserRoleParser.Parse("technical_manager"));
        Assert.Equal(RequestSource.Email, DomainEnumParser.ParseRequestSource("email"));
        Assert.Equal(QuoteStatus.Superseded, DomainEnumParser.ParseQuoteStatus("superseded"));
        Assert.Equal(ChecklistAnswer.NotApplicable, DomainEnumParser.ParseChecklistAnswer("na"));
    }

    [Fact]
    public void UserQualificationsReadLegacyStringsAndStructuredRecords()
    {
        var qualifications = new[]
        {
            new UserQualification("Lifting"),
            new UserQualification("Pressure vessels", new DateOnly(2028, 5, 31))
        };
        var user = AppUser.Create(
            "ADMIN@EXAMPLE.COM",
            "Admin",
            UserRole.Admin,
            qualifications: qualifications);

        Assert.Equal("admin@example.com", user.Email);
        Assert.Equal(qualifications, user.Qualifications);
        Assert.Equal(
            qualifications,
            UserQualification.ParseJson("""["Lifting",{"name":"Pressure vessels","validUntil":"2028-05-31"}]"""));
    }

    [Fact]
    public void ClientRequiresCoordinatePairAndAtMostOnePrimaryContact()
    {
        Assert.Throws<ArgumentException>(() => Client.Create("Client", latitude: 30));

        var client = Client.Create("Client", isCompany: true, hasContract: true, location: "Cairo");
        var contacts = new[]
        {
            ClientContact.Create(client.Id, "First", isPrimary: true),
            ClientContact.Create(client.Id, "Second", isPrimary: true)
        };

        Assert.Throws<InvalidOperationException>(() => client.ReplaceContacts(contacts));
        Assert.Equal("Client", client.Name);
        Assert.True(client.IsCompany);
        Assert.True(client.HasContract);
        Assert.Equal("Cairo", client.Location);
    }

    [Fact]
    public void ClientCompanyAndContractFlagsDefaultToFalse()
    {
        var client = Client.Create("Individual");

        Assert.False(client.IsCompany);
        Assert.False(client.HasContract);
    }

    [Fact]
    public void EquipmentRegistrationRequiresExplicitExpiryExceptionAndTracksDueSoon()
    {
        var today = new DateOnly(2026, 10, 25);
        var expired = today.AddDays(-1);

        Assert.Throws<ArgumentException>(() => Item.Register(
            Guid.NewGuid(), Guid.NewGuid(), "Hoist", expired, true, today: today));

        var equipment = Item.Register(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Hoist",
            today.AddDays(30),
            hasAngle: true,
            manufacturer: "Inspecta",
            serialNumber: "SN-100",
            today: today);

        Assert.True(equipment.IsDueSoon(today));
        Assert.Equal("Hoist", equipment.Type);
        Assert.True(equipment.HasAngle);
    }

    [Fact]
    public void ItemStoresTypeLocationNotesLabelsDatesChecklistAndAttachments()
    {
        var item = Item.Create(Guid.NewGuid(), "Chain block");
        item.UpdateDetails(
            "Chain block",
            "Lifting",
            "Inspecta",
            "CB-1",
            "SN-1",
            "2T",
            ItemType.Gear,
            "Warehouse A / Rack 3",
            "Annual inspection");
        item.UpdateMetadata(
            [" fragile ", "Fragile", "Critical"],
            [ItemDate.Create("Purchased", new DateOnly(2025, 1, 10))],
            [ItemChecklistEntry.Create("Safety latch", true)],
            [ItemAttachment.Create("certificate.pdf", "items/certificate.pdf", "application/pdf", 1024)]);

        Assert.Equal(ItemType.Gear, item.ItemType);
        Assert.Equal("Warehouse A / Rack 3", item.LocationInsideClientLocation);
        Assert.Equal("Annual inspection", item.Notes);
        Assert.Equal(["fragile", "Critical"], item.GetLabels());
        Assert.Equal(new DateOnly(2025, 1, 10), Assert.Single(item.GetDates()).Value);
        Assert.True(Assert.Single(item.GetChecklist()).IsCompleted);
        Assert.Equal("items/certificate.pdf", Assert.Single(item.GetAttachments()).StorageKey);
    }

    [Fact]
    public void RequestKeepsSourceAndHasStableEventIds()
    {
        var actorId = Guid.NewGuid();
        var request = InspectionRequest.Create(
            "Client",
            "Cairo",
            Guid.NewGuid(),
            [RequestItem.Create("Hoist", 1, manufacturer: "Inspecta", location: "Site A")],
            actorId,
            source: RequestSource.Email,
            emailFrom: "requests@example.com",
            hasUnreadClientReply: true);

        Assert.Equal(RequestSource.Email, request.Source);
        Assert.Equal("requests@example.com", request.EmailFrom);
        Assert.NotEqual(Guid.Empty, request.History[0].Id);

        request.MarkClientReplyRead();
        Assert.False(request.HasUnreadClientReply);
    }

    [Fact]
    public void QuoteTotalsAreCalculatedAndDraftIsFrozenWhenSent()
    {
        var quote = Quote.Create(
            Guid.NewGuid(),
            1,
            0,
            DateTimeOffset.UtcNow.AddDays(14),
            Guid.NewGuid());
        var items = new[]
        {
            QuoteItem.Create(Guid.NewGuid(), "Inspection", 2, 1_000, "unit"),
            QuoteItem.Create(null, "Travel", 1, 250, "trip")
        };

        quote.SetDraftDetails(items, "Offer", "Two items");
        Assert.Equal(2_250, quote.AmountPiastres);

        quote.Send();

        Assert.Equal(QuoteStatus.Sent, quote.Status);
        Assert.Equal(2, quote.GetItems().Count);
        Assert.Equal(2_250, quote.ItemsTotalPiastres);
        Assert.Throws<InvalidOperationException>(() => quote.SetDraftDetails(items, "Changed", "Two items"));
        Assert.Throws<ArgumentException>(() => quote.RecordClientResponse(
            QuoteStatus.Countered, "Counter offer", null));
        Assert.Equal(QuoteStatus.Sent, quote.Status);
    }

    [Fact]
    public void CertificateRequiresTypedAnswersAndValidAngleForSubmission()
    {
        var certificate = InspectionCertificate.Create(Guid.NewGuid(), Guid.NewGuid(), hasAngle: true);
        certificate.SetDraft(CompleteDraft(angle: 360m));

        Assert.True(certificate.IsReadyToSubmit());
        certificate.Submit();
        Assert.NotNull(certificate.SubmittedAt);

        var invalidAngleCertificate = InspectionCertificate.Create(Guid.NewGuid(), Guid.NewGuid(), hasAngle: true);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invalidAngleCertificate.SetDraft(CompleteDraft(angle: 360.01m)));

        var angleNotApplicableCertificate = InspectionCertificate.Create(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<ArgumentException>(() =>
            angleNotApplicableCertificate.SetDraft(CompleteDraft(angle: 20m)));
    }

    [Fact]
    public void CertificateComplementsInspectionPeriodAnswerWhenOnlyOneIsProvided()
    {
        var certificate = InspectionCertificate.Create(Guid.NewGuid(), Guid.NewGuid());
        var draft = CompleteDraft() with { Within12Months = null };
        certificate.SetDraft(draft);

        Assert.True(certificate.Draft.Within6Months);
        Assert.False(certificate.Draft.Within12Months);
    }

    [Fact]
    public void ReturnedCertificateMustBeResubmittedBeforeReviewCanRepeat()
    {
        var certificate = InspectionCertificate.Create(Guid.NewGuid(), Guid.NewGuid());
        certificate.SetDraft(CompleteDraft());
        certificate.Submit();
        certificate.ReturnForChanges(Guid.NewGuid(), "Add the missing information.");

        Assert.Throws<InvalidOperationException>(() =>
            certificate.ReturnForChanges(Guid.NewGuid(), "Review again without resubmission."));

        certificate.SetDraft(CompleteDraft() with { Note = "Updated draft" });
        certificate.Submit();
        certificate.Approve(
            Guid.NewGuid(),
            "CERT-002",
            "VERIFY-002",
            "client@example.com",
            """[{"p":[0.1,0.1,0.5,0.5]}]""",
            issuedFor: CertificateIssueSnapshot.Create("Client", "Cairo", "Hoist", "SN-1"));

        Assert.Equal("Client", certificate.IssuedFor?.ClientName);
        Assert.Throws<InvalidOperationException>(() => certificate.SetDraft(CompleteDraft()));
    }

    [Fact]
    public void CertificateAcceptsLegacyNormalizedSignatureFormat()
    {
        var certificate = InspectionCertificate.Create(Guid.NewGuid(), Guid.NewGuid());
        certificate.SetDraft(CompleteDraft());
        certificate.Submit();

        const string firebaseSignature = """[{"p":[0.1,0.1,0.5,0.5]}]""";
        certificate.Approve(
            Guid.NewGuid(),
            "CERT-001",
            "VERIFY-001",
            "client@example.com",
            firebaseSignature);

        Assert.Equal(firebaseSignature, certificate.SignatureStrokesJson);
    }

    [Fact]
    public void SignatureStrokeRequiresNormalizedPoints()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SignaturePoint.Create(1.2, 0.5));
        Assert.Throws<ArgumentException>(() => SignatureStroke.Create([new SignaturePoint(0.1, 0.1)]));
    }

    [Fact]
    public void DemoRequestOnlyAcceptsSupportedLanguages()
    {
        Assert.Throws<ArgumentException>(() => DemoRequest.Create(
            DemoRequestType.Demo,
            "User",
            "user@example.com",
            null,
            null,
            null,
            "fr",
            "website"));
    }

    private static CertificateDraft CompleteDraft(decimal? angle = null) => new()
    {
        StandardOfInspection = "Standard",
        TestType = "Visual and function test",
        ExaminationDate = new DateOnly(2026, 10, 25),
        NextExaminationDate = new DateOnly(2027, 4, 24),
        FunctionCheck = ChecklistAnswer.Pass,
        AngleReadingDegrees = angle,
        FirstExamination = true,
        InstalledCorrectly = true,
        Within6Months = true,
        Within12Months = false,
        ExaminationScheme = true,
        ExceptionalCircumstances = false,
        ExistingDanger = false,
        FutureDanger = false,
        FinalResult = CertificateResult.SafeToOperate
    };
}