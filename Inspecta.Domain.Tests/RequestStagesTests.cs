using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Services;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>How long each stage of a request took, worked out from its status history (calendar time).</summary>
public sealed class RequestStagesTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    private static RequestHistoryResponse At(JobStatus? from, JobStatus to, int minutes, string? note = null) =>
        new(from, to, Actor, Start.AddMinutes(minutes), note);

    private static RequestStageResult Stages(DateTimeOffset now, params RequestHistoryResponse[] history) =>
        RequestStages.Calculate(history, now);

    private static TimelineStage Of(RequestStageResult result, string key) => result.Stages.Single(stage => stage.Key == key);

    [Fact]
    public void TheNormalPathGivesEveryStageItsDuration()
    {
        var result = Stages(Start.AddDays(10),
            At(null, JobStatus.RequestReceived, 0),
            At(JobStatus.RequestReceived, JobStatus.QuoteDraft, 30),
            At(JobStatus.QuoteDraft, JobStatus.QuoteSent, 90),
            At(JobStatus.QuoteSent, JobStatus.Unassigned, 90 + 1440),
            At(JobStatus.Unassigned, JobStatus.Assigned, 1530 + 60),
            At(JobStatus.Assigned, JobStatus.TaskAccepted, 1590 + 10),
            At(JobStatus.TaskAccepted, JobStatus.InProgress, 1600 + 30),
            At(JobStatus.InProgress, JobStatus.CertificateSubmitted, 1630 + 240),
            At(JobStatus.CertificateSubmitted, JobStatus.CertificateApproved, 1870 + 120),
            At(JobStatus.CertificateApproved, JobStatus.SentToClient, 1990 + 45));

        Assert.Equal(90 * 60, Of(result, "quoting").Seconds);
        Assert.Equal(1440 * 60, Of(result, "client_decision").Seconds);
        Assert.Equal(60 * 60, Of(result, "scheduling").Seconds);
        Assert.Equal(40 * 60, Of(result, "waiting_to_start").Seconds);
        Assert.Equal(240 * 60, Of(result, "inspection").Seconds);
        Assert.Equal(120 * 60, Of(result, "review").Seconds);
        Assert.Equal(45 * 60, Of(result, "delivery").Seconds);
        Assert.All(result.Stages, stage => Assert.False(stage.InProgress));
        Assert.Equal(Start, result.StartedAt);
        Assert.Equal(Start.AddMinutes(2035), result.FinishedAt);
        Assert.Equal(new[] { "quoting", "client_decision", "scheduling", "waiting_to_start", "inspection", "review", "delivery" },
            result.Stages.Select(stage => stage.Key));
    }

    [Fact]
    public void CounterOffersExtendTheClientDecisionAndAreCounted()
    {
        var result = Stages(Start.AddDays(10),
            At(null, JobStatus.RequestReceived, 0),
            At(JobStatus.RequestReceived, JobStatus.QuoteDraft, 10),
            At(JobStatus.QuoteDraft, JobStatus.QuoteSent, 100),
            At(JobStatus.QuoteSent, JobStatus.ClientCountered, 200),
            At(JobStatus.ClientCountered, JobStatus.QuoteDraft, 210),
            At(JobStatus.QuoteDraft, JobStatus.CounterOfferSent, 230),
            At(JobStatus.CounterOfferSent, JobStatus.ClientCountered, 300),
            At(JobStatus.ClientCountered, JobStatus.CounterOfferSent, 320),
            At(JobStatus.CounterOfferSent, JobStatus.Unassigned, 500));

        var decision = Of(result, "client_decision");

        Assert.Equal(Start.AddMinutes(100), decision.From);
        Assert.Equal(Start.AddMinutes(500), decision.To);
        Assert.Equal(400 * 60, decision.Seconds);
        Assert.Equal(2, decision.Repeats);
        Assert.Equal(100 * 60, Of(result, "quoting").Seconds);
    }

    [Fact]
    public void AReturnedCertificateExtendsTheReviewAndIsCounted()
    {
        var result = Stages(Start.AddDays(10),
            At(null, JobStatus.RequestReceived, 0),
            At(JobStatus.RequestReceived, JobStatus.Assigned, 5),
            At(JobStatus.Assigned, JobStatus.TaskAccepted, 10),
            At(JobStatus.TaskAccepted, JobStatus.InProgress, 20),
            At(JobStatus.InProgress, JobStatus.CertificateSubmitted, 500),
            At(JobStatus.CertificateSubmitted, JobStatus.CertificateReturned, 600, "Fix the serial number"),
            At(JobStatus.CertificateReturned, JobStatus.InProgress, 650),
            At(JobStatus.InProgress, JobStatus.CertificateSubmitted, 800),
            At(JobStatus.CertificateSubmitted, JobStatus.CertificateApproved, 900));

        var inspection = Of(result, "inspection");
        var review = Of(result, "review");

        Assert.Equal(480 * 60, inspection.Seconds);
        Assert.Equal(0, inspection.Repeats);
        Assert.Equal(Start.AddMinutes(500), review.From);
        Assert.Equal(Start.AddMinutes(900), review.To);
        Assert.Equal(400 * 60, review.Seconds);
        Assert.Equal(1, review.Repeats);
    }

    [Fact]
    public void AJobAssignedWithoutAQuotationHasNoQuotingAndNoClientDecision()
    {
        var result = Stages(Start.AddDays(10),
            At(null, JobStatus.RequestReceived, 0),
            At(JobStatus.RequestReceived, JobStatus.Assigned, 5));

        foreach (var key in new[] { "quoting", "client_decision" })
        {
            var stage = Of(result, key);
            Assert.Null(stage.From);
            Assert.Null(stage.To);
            Assert.Null(stage.Seconds);
            Assert.False(stage.InProgress);
        }

        Assert.Equal(Start, Of(result, "scheduling").From);
        Assert.Equal(5 * 60, Of(result, "scheduling").Seconds);
    }

    [Fact]
    public void ARunningStageHasNoEndAndCountsUntilNow()
    {
        var now = Start.AddMinutes(1000);
        var result = Stages(now,
            At(null, JobStatus.RequestReceived, 0),
            At(JobStatus.RequestReceived, JobStatus.Assigned, 5),
            At(JobStatus.Assigned, JobStatus.TaskAccepted, 10),
            At(JobStatus.TaskAccepted, JobStatus.InProgress, 20));

        var inspection = Of(result, "inspection");

        Assert.True(inspection.InProgress);
        Assert.Null(inspection.To);
        Assert.Equal(980 * 60, inspection.Seconds);
        Assert.Null(Of(result, "review").From);
        Assert.False(Of(result, "review").InProgress);
        Assert.Null(result.FinishedAt);
    }

    [Theory]
    [InlineData(JobStatus.QuoteRejected)]
    [InlineData(JobStatus.ClientDeclined)]
    public void ARequestTurnedDownEndsEarlyAndTheLaterStagesStayEmpty(JobStatus end)
    {
        var result = Stages(Start.AddDays(10),
            At(null, JobStatus.RequestReceived, 0),
            At(JobStatus.RequestReceived, JobStatus.QuoteDraft, 10),
            At(JobStatus.QuoteDraft, JobStatus.QuoteSent, 20),
            At(JobStatus.QuoteSent, end, 50, "Too expensive"));

        var decision = Of(result, "client_decision");

        Assert.Equal(Start.AddMinutes(50), decision.To);
        Assert.Equal(30 * 60, decision.Seconds);
        Assert.False(decision.InProgress);
        Assert.Equal(Start.AddMinutes(50), result.FinishedAt);
        Assert.Null(Of(result, "scheduling").From);
    }

    [Fact]
    public void AnEditOfTheRequestDoesNotChangeTheStages()
    {
        var plain = Stages(Start.AddDays(10),
            At(null, JobStatus.RequestReceived, 0),
            At(JobStatus.RequestReceived, JobStatus.QuoteDraft, 30),
            At(JobStatus.QuoteDraft, JobStatus.QuoteSent, 90));
        var edited = Stages(Start.AddDays(10),
            At(null, JobStatus.RequestReceived, 0),
            At(JobStatus.RequestReceived, JobStatus.RequestReceived, 5, "Machine added: Crane"),
            At(JobStatus.RequestReceived, JobStatus.QuoteDraft, 30),
            At(JobStatus.QuoteDraft, JobStatus.QuoteDraft, 40, "Machine changed: Crane"),
            At(JobStatus.QuoteDraft, JobStatus.QuoteSent, 90));

        Assert.Equal(plain.Stages, edited.Stages);
    }

    [Fact]
    public void AnEmptyHistoryGivesNoStagesAndNoStart()
    {
        var result = Stages(Start);

        Assert.Null(result.StartedAt);
        Assert.Null(result.FinishedAt);
        Assert.All(result.Stages, stage => Assert.Null(stage.Seconds));
    }
}
