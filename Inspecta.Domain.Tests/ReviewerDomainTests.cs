using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The reviewer of a job: it starts with a default, can be changed until the certificates are approved, and every change is written down.</summary>
public sealed class ReviewerDomainTests
{
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Coordinator = Guid.NewGuid();
    private static readonly Guid First = Guid.NewGuid();
    private static readonly Guid Second = Guid.NewGuid();

    private static InspectionRequest NewRequest() =>
        InspectionRequest.Create("Acme", "Cairo", null, [RequestItem.Create("Crane", 1)], Coordinator, managerId: Company);

    private static InspectionRequest AtReview()
    {
        var request = NewRequest();
        request.TransitionTo(JobStatus.QuoteDraft, Coordinator);
        request.TransitionTo(JobStatus.QuoteSent, Coordinator);
        request.TransitionTo(JobStatus.Unassigned, Coordinator);
        request.AssignInspector(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1), Coordinator);
        request.TransitionTo(JobStatus.TaskAccepted, Coordinator);
        request.TransitionTo(JobStatus.InProgress, Coordinator);
        request.TransitionTo(JobStatus.CertificateSubmitted, Coordinator);
        return request;
    }

    [Fact]
    public void ANewRequestHasNoReviewerAndTheDefaultIsSetWithoutAnyHistory()
    {
        var request = NewRequest();
        var before = request.History.Count;

        Assert.Null(request.ReviewerId);
        request.UseReviewer(First);

        Assert.Equal(First, request.ReviewerId);
        Assert.Equal(before, request.History.Count);
        Assert.Throws<ArgumentException>(() => request.UseReviewer(Guid.Empty));
    }

    [Fact]
    public void ChangingTheReviewerKeepsTheStatusAndTheHistoryNamesBoth()
    {
        var request = NewRequest();
        request.UseReviewer(First);

        request.ChangeReviewer(Second, Coordinator, "Hassan", "Mona", "Hassan is away");

        Assert.Equal(Second, request.ReviewerId);
        Assert.Equal(JobStatus.RequestReceived, request.Status);
        var entry = request.History.OrderBy(item => item.OccurredAt).Last();
        Assert.Equal(entry.FromStatus, entry.ToStatus);
        Assert.Equal(Coordinator, entry.ActorId);
        Assert.StartsWith(InspectionRequest.ReviewerNotePrefix, entry.Note);
        Assert.Contains("Hassan", entry.Note);
        Assert.Contains("Mona", entry.Note);
        Assert.Contains("away", entry.Note);
    }

    [Fact]
    public void TheSameReviewerOrNobodyIsRefused()
    {
        var request = NewRequest();
        request.UseReviewer(First);

        Assert.Throws<InvalidOperationException>(() => request.ChangeReviewer(First, Coordinator, "A", "A"));
        Assert.Throws<ArgumentException>(() => request.ChangeReviewer(Guid.Empty, Coordinator, "A", "B"));
        Assert.Throws<ArgumentException>(() => request.ChangeReviewer(Second, Guid.Empty, "A", "B"));
        Assert.Equal(First, request.ReviewerId);
    }

    [Theory]
    [InlineData(JobStatus.CertificateSubmitted)]
    [InlineData(JobStatus.CertificateReturned)]
    public void TheReviewerCanChangeWhileTheCertificatesAreStillUnderReview(JobStatus status)
    {
        var request = AtReview();
        if (status == JobStatus.CertificateReturned) request.TransitionTo(JobStatus.CertificateReturned, Coordinator, "Fix the serial");
        request.UseReviewer(First);

        request.ChangeReviewer(Second, Coordinator, "A", "B");

        Assert.Equal(Second, request.ReviewerId);
        Assert.Equal(status, request.Status);
    }

    [Fact]
    public void TheReviewerCannotChangeOnceTheCertificatesAreApproved()
    {
        var request = AtReview();
        request.UseReviewer(First);
        request.TransitionTo(JobStatus.CertificateApproved, First);

        Assert.Throws<InvalidOperationException>(() => request.ChangeReviewer(Second, Coordinator, "A", "B"));
        Assert.Equal(First, request.ReviewerId);
    }

    [Theory]
    [InlineData(JobStatus.QuoteRejected)]
    [InlineData(JobStatus.ClientDeclined)]
    public void AFinishedRequestKeepsItsReviewer(JobStatus end)
    {
        var request = NewRequest();
        request.TransitionTo(JobStatus.QuoteDraft, Coordinator);
        request.TransitionTo(JobStatus.QuoteSent, Coordinator);
        request.TransitionTo(end, Coordinator, "Too expensive");
        request.UseReviewer(First);

        Assert.Throws<InvalidOperationException>(() => request.ChangeReviewer(Second, Coordinator, "A", "B"));
    }

    [Fact]
    public void AnEmailThatIsStillAMaybeHasNoReviewerToChange()
    {
        var maybe = InspectionRequest.CreateFromEmail(
            Company, Coordinator,
            new IncomingEmail("1", 1, "Hello", null, "x@y.com", "X", "Hi", [], [], [], [], [], "<1@x>", null, null, DateTimeOffset.UtcNow),
            triage: RequestTriage.Maybe);
        maybe.UseReviewer(First);

        Assert.Throws<InvalidOperationException>(() => maybe.ChangeReviewer(Second, Coordinator, "A", "B"));
    }
}
