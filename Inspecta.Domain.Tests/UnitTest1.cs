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

public sealed class TimesheetTests
{
    [Fact]
    public void PricingUsesPaidMinutesAndRoundsToNearestPiastre()
    {
        var timesheet = TimesheetEntry.Priced(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new Dictionary<string, int> { ["inspection"] = 31, ["unpaidBreak"] = 60 },
            new Dictionary<string, long> { ["transport"] = 250 },
            100);

        Assert.Equal(52, timesheet.PricePiastres);
        Assert.Equal(302, timesheet.TotalPiastres);
    }

    [Fact]
    public void ApprovedTimesheetCannotBeChanged()
    {
        var timesheet = TimesheetEntry.Priced(
            Guid.NewGuid(), Guid.NewGuid(), new Dictionary<string, int> { ["inspection"] = 60 },
            new Dictionary<string, long>(), 100);
        timesheet.Approve(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => timesheet.Update(
            new Dictionary<string, int> { ["inspection"] = 30 }, new Dictionary<string, long>(), 100));
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
                ["exceptionalCircumstances"] = false
            },
            ChecklistAnswer.Pass,
            CertificateResult.SafeToOperate);
        certificate.Submit();

        Assert.Throws<ArgumentException>(() => certificate.Approve(Guid.NewGuid(), "CERT-001", "VERIFY-001", "client@example.com", "[]"));
        certificate.Approve(Guid.NewGuid(), "CERT-001", "VERIFY-001", "client@example.com", "[[0.1,0.1,0.5,0.5]]");

        Assert.Equal("CERT-001", certificate.CertificateNumber);
    }
}