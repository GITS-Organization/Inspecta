using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The one list of events of a request: every source once, in order, with who did it and the time since the one before.</summary>
public sealed class RequestTimelineBuilderTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Coordinator = Guid.NewGuid();
    private static readonly Guid Manager = Guid.NewGuid();

    private static InspectionRequest NewRequest(params (JobStatus? From, JobStatus To, int Minutes, string? Note)[] history)
    {
        var request = InspectionRequest.Create("Acme", "Cairo", null, [RequestItem.Create("Crane", 1)], Coordinator, managerId: Manager);
        request.History.Clear();
        foreach (var (from, to, minutes, note) in history)
            request.History.Add(JobStatusHistory.Create(request.Id, from, to, Coordinator, Start.AddMinutes(minutes), note));
        return request;
    }

    private static RequestTimelineSources Sources(
        InspectionRequest request,
        IReadOnlyList<Quote>? quotes = null,
        IReadOnlyList<OutboundEmailSummary>? emails = null,
        CertificateDelivery? delivery = null) =>
        new(request, quotes ?? [], emails ?? [], [], delivery, new Dictionary<Guid, string> { [Coordinator] = "Mona Coordinator" });

    [Fact]
    public void EventsAreOldestFirstWithTheActorAndTheTimeSinceTheOneBefore()
    {
        var request = NewRequest(
            (null, JobStatus.RequestReceived, 0, null),
            (JobStatus.RequestReceived, JobStatus.RequestReceived, 5, "Machine added: Crane"),
            (JobStatus.RequestReceived, JobStatus.QuoteDraft, 30, null));

        var timeline = RequestTimelineBuilder.Build(Sources(request), Start.AddDays(1));

        Assert.Equal(new[] { "status", "edit", "status" }, timeline.Events.Select(item => item.Kind));
        Assert.Equal(new long[] { 0, 5 * 60, 25 * 60 }, timeline.Events.Select(item => item.SincePreviousSeconds));
        Assert.Equal("Machine added: Crane", timeline.Events[1].Title);
        Assert.All(timeline.Events, item => Assert.Equal("Mona Coordinator", item.Actor!.Name));
        Assert.Equal(Start, timeline.StartedAt);
    }

    [Fact]
    public void AnOfferSentIsListedOnceAndCarriesTheQuotationFigures()
    {
        var sentAt = DateTimeOffset.UtcNow;
        var request = NewRequest((null, JobStatus.RequestReceived, 0, null));
        request.History.Add(JobStatusHistory.Create(request.Id, JobStatus.RequestReceived, JobStatus.QuoteSent, Coordinator, sentAt, null));
        var quote = Quote.Create(request.Id, 1, 0, sentAt.AddDays(10), Coordinator);
        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, 500_000, "Per Visit")], null, null);
        quote.SetDiscount(10m);
        quote.Send();

        var timeline = RequestTimelineBuilder.Build(Sources(request, [quote]), sentAt.AddHours(1));

        var offer = Assert.Single(timeline.Events, item => item.Title == "Quotation sent to the client");
        Assert.Contains("total 4,500.00", offer.Detail);
        Assert.Contains("10% discount of 500.00", offer.Detail);
        Assert.DoesNotContain(timeline.Events, item => item.Kind == "quotation");
    }

    [Fact]
    public void ASentQuotationThatTheHistoryDoesNotShowGetsItsOwnEvent()
    {
        var request = NewRequest((null, JobStatus.RequestReceived, 0, null));
        var quote = Quote.Create(request.Id, 1, 0, DateTimeOffset.UtcNow.AddDays(10), Coordinator);
        quote.SetDraftDetails([QuoteItem.Create(Guid.NewGuid(), "Crane", 1, 100_000, "Per Visit")], null, null);
        quote.Send();

        var timeline = RequestTimelineBuilder.Build(Sources(request, [quote]), DateTimeOffset.UtcNow);

        Assert.Single(timeline.Events, item => item.Kind == "quotation" && item.Title == "Quotation revision 1 sent");
    }

    [Fact]
    public void RevisionsEmailsReviewCommentsAndTheDeliveryAreAllOnTheList()
    {
        var request = NewRequest((null, JobStatus.RequestReceived, 0, null));
        request.ReviewComments.Add(ReviewComment.Create(request.Id, null, Manager, "Hassan Manager", "Fix the serial number", Start.AddHours(5)));
        var revision = Quote.Create(request.Id, 2, 90_000, DateTimeOffset.UtcNow.AddDays(10), Coordinator, createdAt: Start.AddHours(2));
        var sent = new OutboundEmailSummary(revision.Id, OutboundEmailKind.Offer, OutboundEmailStatus.Sent, "client@acme.test", 1, Start.AddHours(3), null, Start.AddHours(3), Subject: "Offer");
        var failed = new OutboundEmailSummary(revision.Id, OutboundEmailKind.Offer, OutboundEmailStatus.Failed, "client@acme.test", 1, null, "Mailbox full", Start.AddHours(4));
        var pending = new OutboundEmailSummary(revision.Id, OutboundEmailKind.Offer, OutboundEmailStatus.Pending, "client@acme.test", 0, null, null, Start.AddHours(4));
        var delivery = CertificateDelivery.Create(Manager, request.Id, "client@acme.test", "Certificates", "Body", "<id@x>", ["a.pdf"]);
        delivery.MarkSent();

        var timeline = RequestTimelineBuilder.Build(
            new RequestTimelineSources(request, [revision], [sent, failed, pending], [], delivery, new Dictionary<Guid, string> { [Manager] = "Hassan Manager" }),
            DateTimeOffset.UtcNow);

        Assert.Equal(
            new[] { "Quotation revision 2 created", "Offer email sent to client@acme.test", "Offer email to client@acme.test failed", "Review comment" },
            timeline.Events.Where(item => item.Kind != "status" && item.Kind != "delivery").Select(item => item.Title));
        Assert.Equal("Mailbox full", timeline.Events.Single(item => item.Title.EndsWith("failed")).Detail);
        Assert.Equal("Hassan Manager", timeline.Events.Single(item => item.Kind == "review").Actor!.Name);
        Assert.Single(timeline.Events, item => item.Kind == "delivery");
        Assert.Equal(1, timeline.Counters.QuoteRevisions);
    }

    [Fact]
    public void CountersCountOffersReturnsAndInspectorChanges()
    {
        var request = NewRequest(
            (null, JobStatus.RequestReceived, 0, null),
            (JobStatus.RequestReceived, JobStatus.QuoteDraft, 1, null),
            (JobStatus.QuoteDraft, JobStatus.QuoteSent, 2, null),
            (JobStatus.QuoteSent, JobStatus.ClientCountered, 3, null),
            (JobStatus.ClientCountered, JobStatus.QuoteDraft, 3, null),
            (JobStatus.QuoteDraft, JobStatus.CounterOfferSent, 4, null),
            (JobStatus.CounterOfferSent, JobStatus.Unassigned, 5, null),
            (JobStatus.Unassigned, JobStatus.Assigned, 6, null),
            (JobStatus.Assigned, JobStatus.Assigned, 7, "Inspection date changed from 2026-11-03 to 2026-11-04."),
            (JobStatus.Assigned, JobStatus.Assigned, 8, "Another inspector"),
            (JobStatus.Assigned, JobStatus.TaskAccepted, 9, null),
            (JobStatus.TaskAccepted, JobStatus.Assigned, 10, "Handed over"),
            (JobStatus.Assigned, JobStatus.TaskAccepted, 11, null),
            (JobStatus.TaskAccepted, JobStatus.InProgress, 12, null),
            (JobStatus.InProgress, JobStatus.CertificateSubmitted, 13, null),
            (JobStatus.CertificateSubmitted, JobStatus.CertificateReturned, 14, "Fix"),
            (JobStatus.CertificateReturned, JobStatus.InProgress, 15, null),
            (JobStatus.InProgress, JobStatus.CertificateSubmitted, 16, null),
            (JobStatus.CertificateSubmitted, JobStatus.CertificateReturned, 17, "Again"));

        var counters = RequestTimelineBuilder.Build(Sources(request), Start.AddDays(1)).Counters;

        Assert.Equal(1, counters.CounterOffers);
        Assert.Equal(2, counters.CertificatesReturned);
        Assert.Equal(2, counters.InspectorChanges);
    }

    [Fact]
    public void ThePlaceholderQuotationOfAManDayJobIsNotAnOfferOrAnAnswer()
    {
        var request = NewRequest((null, JobStatus.RequestReceived, 0, null), (JobStatus.RequestReceived, JobStatus.Assigned, 0, null));
        var placeholder = Quote.CreateAccepted(request.Id, Coordinator, "MD");

        var timeline = RequestTimelineBuilder.Build(Sources(request, [placeholder]), Start.AddHours(1));

        Assert.DoesNotContain(timeline.Events, item => item.Kind == "quotation");
        Assert.Equal("Request received", timeline.Events[0].Title);
    }

    [Fact]
    public void DismissingTheClientsReplyIsShownAsThatAndNotCountedAsACounterOffer()
    {
        var request = NewRequest(
            (null, JobStatus.RequestReceived, 0, null),
            (JobStatus.RequestReceived, JobStatus.QuoteDraft, 1, null),
            (JobStatus.QuoteDraft, JobStatus.CounterOfferSent, 2, null),
            (JobStatus.CounterOfferSent, JobStatus.ClientCountered, 3, null),
            (JobStatus.ClientCountered, JobStatus.CounterOfferSent, 4, "The client's reply was dismissed."));

        var timeline = RequestTimelineBuilder.Build(Sources(request), Start.AddDays(1));

        Assert.Equal(1, timeline.Counters.CounterOffers);
        Assert.Contains(timeline.Events, item => item.Title.StartsWith("Client's reply dismissed"));
    }

    [Fact]
    public void ATransferIsAnEventNamedAsSuchAndDoesNotChangeTheStages()
    {
        var request = NewRequest(
            (null, JobStatus.RequestReceived, 0, null),
            (JobStatus.RequestReceived, JobStatus.RequestReceived, 5, "Transferred from Mona to Hassan. Cranes are his."),
            (JobStatus.RequestReceived, JobStatus.QuoteDraft, 30, null));

        var timeline = RequestTimelineBuilder.Build(Sources(request), Start.AddDays(1));

        var transfer = Assert.Single(timeline.Events, item => item.Title == "Request transferred");
        Assert.Equal("status", transfer.Kind);
        Assert.Contains("Mona to Hassan", transfer.Detail);
        var quoting = timeline.Stages.Single(stage => stage.Key == "quoting");
        Assert.Equal(Start, quoting.From);
        Assert.True(quoting.InProgress);
    }

    [Fact]
    public void ATransferOfAnAssignedJobIsNotCountedAsAnInspectorChange()
    {
        var request = NewRequest(
            (null, JobStatus.RequestReceived, 0, null),
            (JobStatus.RequestReceived, JobStatus.Assigned, 1, null),
            (JobStatus.Assigned, JobStatus.Assigned, 5, "Transferred from Mona to Hassan."));

        var timeline = RequestTimelineBuilder.Build(Sources(request), Start.AddDays(1));

        Assert.Equal(0, timeline.Counters.InspectorChanges);
        Assert.Contains(timeline.Events, item => item.Title == "Request transferred");
        Assert.DoesNotContain(timeline.Events, item => item.Title == "Job handed to another inspector");
    }

    [Fact]
    public void AChangeOfReviewerIsAnEventAndNeverAnInspectorChangeEvenOnAnAssignedJob()
    {
        var request = NewRequest(
            (null, JobStatus.RequestReceived, 0, null),
            (JobStatus.RequestReceived, JobStatus.Assigned, 1, null),
            (JobStatus.Assigned, JobStatus.Assigned, 5, "Reviewer changed from Hassan to Mona. He is away."));

        var timeline = RequestTimelineBuilder.Build(Sources(request), Start.AddDays(1));

        Assert.Equal(0, timeline.Counters.InspectorChanges);
        var change = Assert.Single(timeline.Events, item => item.Title == "Reviewer changed");
        Assert.Contains("Hassan to Mona", change.Detail);
        Assert.DoesNotContain(timeline.Events, item => item.Title == "Job handed to another inspector");
    }

    [Fact]
    public void ARequestStillOpenCountsItsTotalUntilNow()
    {
        var request = NewRequest((null, JobStatus.RequestReceived, 0, null), (JobStatus.RequestReceived, JobStatus.QuoteDraft, 10, null));

        var timeline = RequestTimelineBuilder.Build(Sources(request), Start.AddMinutes(100));

        Assert.Null(timeline.FinishedAt);
        Assert.Equal(100 * 60, timeline.TotalSeconds);
    }

    [Fact]
    public void AFinishedRequestStopsItsTotalAtTheDelivery()
    {
        var request = NewRequest(
            (null, JobStatus.RequestReceived, 0, null),
            (JobStatus.RequestReceived, JobStatus.Assigned, 5, null),
            (JobStatus.Assigned, JobStatus.SentToClient, 600, null));

        var timeline = RequestTimelineBuilder.Build(Sources(request), Start.AddDays(30));

        Assert.Equal(Start.AddMinutes(600), timeline.FinishedAt);
        Assert.Equal(600 * 60, timeline.TotalSeconds);
    }
}
