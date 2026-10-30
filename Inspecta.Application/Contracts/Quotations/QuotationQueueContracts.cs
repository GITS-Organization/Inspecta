using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Quotations;

/// <summary>
/// The lists an accepted quotation moves through once the client said yes. <see cref="Certify"/> holds every job whose
/// inspection is done; <see cref="Review"/>, <see cref="Returned"/>, <see cref="Approved"/> and <see cref="Delivered"/>
/// narrow it to the jobs waiting on one person. A row's own <c>queue</c> stays <c>certify</c> for all of them.
/// </summary>
public enum QuotationQueue
{
    /// <summary>Accepted and waiting for the coordinator to give it to an inspector.</summary>
    Unassigned,

    /// <summary>Given to an inspector, who has not finished the inspection yet.</summary>
    Assigned,

    /// <summary>Inspection done: the certificate is being prepared, reviewed and sent.</summary>
    Certify,

    /// <summary>Every machine submitted, waiting for the technical manager to approve or return.</summary>
    Review,

    /// <summary>Sent back by the technical manager, waiting for the inspector to fix and submit again.</summary>
    Returned,

    /// <summary>Every certificate approved, waiting for the coordinator to hand them to the client.</summary>
    Approved,

    /// <summary>Handed to the client.</summary>
    Delivered
}

public static class QuotationQueues
{
    private static readonly JobStatus[] UnassignedStatuses = [JobStatus.Unassigned];
    private static readonly JobStatus[] AssignedStatuses = [JobStatus.Assigned, JobStatus.TaskAccepted, JobStatus.InProgress];

    private static readonly JobStatus[] CertifyStatuses =
    [
        JobStatus.CertificateSubmitted, JobStatus.CertificateReturned, JobStatus.CertificateApproved, JobStatus.SentToClient
    ];

    /// <summary>The request statuses that put a quotation in the queue.</summary>
    public static IReadOnlyCollection<JobStatus> Statuses(QuotationQueue queue) => queue switch
    {
        QuotationQueue.Unassigned => UnassignedStatuses,
        QuotationQueue.Assigned => AssignedStatuses,
        QuotationQueue.Review => [JobStatus.CertificateSubmitted],
        QuotationQueue.Returned => [JobStatus.CertificateReturned],
        QuotationQueue.Approved => [JobStatus.CertificateApproved],
        QuotationQueue.Delivered => [JobStatus.SentToClient],
        _ => CertifyStatuses
    };

    /// <summary>Every request status that is in one of the queues.</summary>
    public static IReadOnlyCollection<JobStatus> AllStatuses { get; } =
        [.. UnassignedStatuses, .. AssignedStatuses, .. CertifyStatuses];

    /// <summary>
    /// The queue a request status belongs to (<c>unassigned</c>, <c>assigned</c> or <c>certify</c>), or null while the
    /// quotation is still being negotiated.
    /// </summary>
    public static QuotationQueue? Of(JobStatus status) =>
        UnassignedStatuses.Contains(status) ? QuotationQueue.Unassigned
        : AssignedStatuses.Contains(status) ? QuotationQueue.Assigned
        : CertifyStatuses.Contains(status) ? QuotationQueue.Certify
        : null;
}

/// <summary>Filters and paging for the queue list.</summary>
/// <param name="ManagerId">Admin only: narrow the global view to one team.</param>
/// <param name="InspectorId">Only quotations given to this inspector (not for the unassigned queue).</param>
public sealed record QuotationQueueQuery(
    Guid? ManagerId = null,
    Guid? InspectorId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = QuoteListQuery.DefaultPageSize);
