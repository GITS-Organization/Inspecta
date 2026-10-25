using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

public sealed class JobStatusHistory
{
    private JobStatusHistory() { }

    public Guid Id { get; private set; }
    public string? LegacyId { get; private set; }
    public Guid RequestId { get; private set; }
    public JobStatus? FromStatus { get; private set; }
    public JobStatus ToStatus { get; private set; }
    public Guid ActorId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset Timestamp => OccurredAt;
    public string? Note { get; private set; }

    public static JobStatusHistory Create(
        Guid requestId,
        JobStatus? fromStatus,
        JobStatus toStatus,
        Guid actorId,
        DateTimeOffset occurredAt,
        string? note,
        string? legacyId = null)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("Request is required.", nameof(requestId));
        if (actorId == Guid.Empty)
            throw new ArgumentException("Actor is required.", nameof(actorId));
        if (!Enum.IsDefined(toStatus) || (fromStatus is { } previous && !Enum.IsDefined(previous)))
            throw new ArgumentOutOfRangeException(nameof(toStatus));

        return new JobStatusHistory
        {
            Id = Guid.NewGuid(),
            LegacyId = legacyId,
            RequestId = requestId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorId = actorId,
            OccurredAt = occurredAt,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };
    }
}