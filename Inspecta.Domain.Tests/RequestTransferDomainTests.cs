using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>Handing a request to another coordinator: the owner changes, the status does not, and the history says who and to whom.</summary>
public sealed class RequestTransferDomainTests
{
    private static readonly Guid Manager = Guid.NewGuid();
    private static readonly Guid First = Guid.NewGuid();
    private static readonly Guid Second = Guid.NewGuid();

    private static InspectionRequest NewRequest() =>
        InspectionRequest.Create("Acme", "Cairo", null, [RequestItem.Create("Crane", 1)], First, managerId: Manager);

    [Fact]
    public void TheRequestChangesOwnerKeepsItsStatusAndTheHistoryNamesBoth()
    {
        var request = NewRequest();
        var before = request.History.Count;

        request.TransferTo(Second, First, "Mona", "Hassan", "Hassan handles crane requests");

        Assert.Equal(Second, request.CoordinatorId);
        Assert.Equal(JobStatus.RequestReceived, request.Status);
        var entry = request.History.OrderBy(item => item.OccurredAt).Last();
        Assert.Equal(before + 1, request.History.Count);
        Assert.Equal(entry.FromStatus, entry.ToStatus);
        Assert.Equal(First, entry.ActorId);
        Assert.StartsWith(InspectionRequest.TransferNotePrefix, entry.Note);
        Assert.Contains("Mona", entry.Note);
        Assert.Contains("Hassan", entry.Note);
        Assert.Contains("crane requests", entry.Note);
    }

    [Fact]
    public void ARequestInQuotationDraftCanBeTransferred()
    {
        var request = NewRequest();
        request.TransitionTo(JobStatus.QuoteDraft, First);

        request.TransferTo(Second, First, "Mona", "Hassan");

        Assert.Equal(Second, request.CoordinatorId);
        Assert.Equal(JobStatus.QuoteDraft, request.Status);
    }

    [Fact]
    public void TransferringToTheCurrentOwnerOrToNobodyIsRefused()
    {
        var request = NewRequest();

        Assert.Throws<InvalidOperationException>(() => request.TransferTo(First, First, "Mona", "Mona"));
        Assert.Throws<ArgumentException>(() => request.TransferTo(Guid.Empty, First, "Mona", "Nobody"));
        Assert.Throws<ArgumentException>(() => request.TransferTo(Second, Guid.Empty, "Mona", "Hassan"));
        Assert.Equal(First, request.CoordinatorId);
    }

    [Theory]
    [InlineData(JobStatus.QuoteRejected)]
    [InlineData(JobStatus.ClientDeclined)]
    public void ARequestTurnedDownCannotBeTransferred(JobStatus end)
    {
        var request = NewRequest();
        request.TransitionTo(JobStatus.QuoteDraft, First);
        request.TransitionTo(JobStatus.QuoteSent, First);
        request.TransitionTo(end, First, "Too expensive");

        Assert.Throws<InvalidOperationException>(() => request.TransferTo(Second, First, "Mona", "Hassan"));
        Assert.Equal(First, request.CoordinatorId);
    }

    [Fact]
    public void AMaybeEmailMustBeConfirmedBeforeItCanBeTransferred()
    {
        var maybe = InspectionRequest.CreateFromEmail(
            Manager, First,
            new IncomingEmail("1", 1, "Hello", null, "x@y.com", "X", "Hi", [], [], [], [], [], "<1@x>", null, null, DateTimeOffset.UtcNow),
            triage: RequestTriage.Maybe);

        Assert.Throws<InvalidOperationException>(() => maybe.TransferTo(Second, First, "Mona", "Hassan"));
    }
}
