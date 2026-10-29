using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Requests;

/// <summary>Inspection request with its items and status history.</summary>
/// <remarks>
/// Requests created from email start with empty <c>ClientName</c>, <c>Location</c> and <c>Items</c>;
/// <c>MissingFields</c> lists what the coordinator still has to fill in before a quote can be drafted.
/// </remarks>
public sealed record RequestResponse(
    Guid Id,
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
    RequestSource Source,
    string? EmailFrom,
    string? EmailPreview,
    IReadOnlyList<RequestItemResponse> Items,
    IReadOnlyList<RequestHistoryResponse> History,
    Guid? ManagerId,
    string? EmailFromName,
    string? AccessNotes,
    IReadOnlyList<string> MissingFields,
    bool CanEditDetails,
    DateTimeOffset UpdatedAt,
    RequestTriage Triage = RequestTriage.Request,
    DateTimeOffset? MaybeExpiresAt = null,
    Quotations.QuoteResponse? Quotation = null);

/// <summary>Equipment requested for inspection.</summary>
public sealed record RequestItemResponse(
    Guid Id,
    Guid? EquipmentId,
    string Name,
    string? Type,
    string? Capacity,
    int Quantity,
    string? SerialNumber,
    string? IssueDescription,
    string? Location = null);

/// <summary>One recorded request status transition.</summary>
public sealed record RequestHistoryResponse(JobStatus? From, JobStatus To, Guid ActorId, DateTimeOffset At, string? Note);
