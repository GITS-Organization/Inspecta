using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

public sealed class Quote
{
    private Quote() { }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public int Version { get; private set; }
    public long AmountPiastres { get; private set; }
    public DateTimeOffset ValidUntil { get; private set; }
    public QuoteStatus Status { get; private set; }
    public string? ClientReply { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Quote Create(Guid requestId, int version, long amountPiastres, DateTimeOffset validUntil, Guid createdBy)
    {
        if (requestId == Guid.Empty || createdBy == Guid.Empty) throw new ArgumentException("Request and creator are required.");
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        if (amountPiastres < 0) throw new ArgumentOutOfRangeException(nameof(amountPiastres));
        if (validUntil <= DateTimeOffset.UtcNow) throw new ArgumentException("Quote expiry must be in the future.", nameof(validUntil));
        return new Quote
        {
            Id = Guid.NewGuid(), RequestId = requestId, Version = version,
            AmountPiastres = amountPiastres, ValidUntil = validUntil, Status = QuoteStatus.Draft,
            CreatedBy = createdBy, CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Send()
    {
        if (Status != QuoteStatus.Draft) throw new InvalidOperationException("Only draft quotes can be sent.");
        Status = QuoteStatus.Sent;
    }

    public void RecordClientDecision(QuoteStatus decision, string? note = null)
    {
        if (Status != QuoteStatus.Sent) throw new InvalidOperationException("Only sent quotes can receive a client decision.");
        if (decision is not (QuoteStatus.Countered or QuoteStatus.Accepted or QuoteStatus.Rejected))
            throw new ArgumentException("Decision must be countered, accepted, or rejected.", nameof(decision));
        if (decision == QuoteStatus.Rejected && string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("A rejection reason is required.", nameof(note));
        Status = decision;
        ClientReply = note?.Trim();
    }
}