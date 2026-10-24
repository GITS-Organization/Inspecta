using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Requests;

/// <summary>Inspection request with its items and status history.</summary>
public sealed record RequestResponse(
    Guid Id,
    string? LegacyId,
    Guid? ClientId,
    string ClientName,
    string Location,
    string? Subject,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? PreferredDate,
    JobStatus Status,
    Guid? CoordinatorId,
    Guid? InspectorId,
    DateTimeOffset? ScheduledAt,
    IReadOnlyList<RequestItemResponse> Items,
    IReadOnlyList<RequestHistoryResponse> History);

/// <summary>Equipment requested for inspection.</summary>
public sealed record RequestItemResponse(Guid Id, Guid? EquipmentId, string Name, string? Type, string? Capacity, int Quantity, string? SerialNumber);

/// <summary>One recorded request status transition.</summary>
public sealed record RequestHistoryResponse(JobStatus? From, JobStatus To, Guid ActorId, DateTimeOffset At, string? Note);