using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Quotations;

/// <summary>The three lists an accepted quotation moves through once the client said yes.</summary>
public enum QuotationQueue
{
    /// <summary>Accepted and waiting for the coordinator to give it to an inspector.</summary>
    Unassigned,

    /// <summary>Given to an inspector, who has not finished the inspection yet.</summary>
    Assigned,

    /// <summary>Inspection done: the certificate is being prepared, reviewed and sent.</summary>
    Certify
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
        _ => CertifyStatuses
    };

    /// <summary>The queue a request status belongs to, or null while the quotation is still being negotiated.</summary>
    public static QuotationQueue? Of(JobStatus status) =>
        Enum.GetValues<QuotationQueue>().Cast<QuotationQueue?>().FirstOrDefault(queue => Statuses(queue!.Value).Contains(status));
}

/// <summary>Filters and paging for one queue.</summary>
/// <param name="ManagerId">Admin only: narrow the global view to one team.</param>
/// <param name="InspectorId">Only quotations given to this inspector (not for the unassigned queue).</param>
public sealed record QuotationQueueQuery(
    Guid? ManagerId = null,
    Guid? InspectorId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = QuoteListQuery.DefaultPageSize);
