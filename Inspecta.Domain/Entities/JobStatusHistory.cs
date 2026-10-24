using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

public sealed class JobStatusHistory(Guid requestId, JobStatus? fromStatus, JobStatus toStatus, Guid actorId, DateTimeOffset occurredAt, string? note)
{
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; } = requestId;
    public JobStatus? FromStatus { get; private set; } = fromStatus;
    public JobStatus ToStatus { get; private set; } = toStatus;
    public Guid ActorId { get; private set; } = actorId;
    public DateTimeOffset OccurredAt { get; private set; } = occurredAt;
    public string? Note { get; private set; } = note;
}