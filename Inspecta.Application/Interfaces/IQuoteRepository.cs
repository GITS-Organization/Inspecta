using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

/// <summary>Which quotations a list call may return, beyond the tenant filter applied by the data layer.</summary>
/// <param name="CoordinatorId">
/// Coordinator scope: quotations of manual requests stay visible to the whole team, those of email requests only to
/// the coordinator that received them.
/// </param>
/// <param name="ManagerId">Only quotations of this team (used by Admin to narrow the global view).</param>
/// <param name="Open">True for open quotations, false for closed ones (accepted or rejected).</param>
/// <param name="Sent">Whether the quotation was sent to the client.</param>
/// <param name="Accepted">True for accepted, false for rejected, among closed quotations.</param>
/// <param name="InspectorId">Only quotations whose request is assigned to this inspector.</param>
/// <param name="Assigned">True for requests that have an inspector, false for those still without one.</param>
/// <param name="RequestStatuses">Only quotations whose request is in one of these statuses.</param>
/// <param name="CurrentOnly">Leave out revisions that a newer one replaced.</param>
/// <param name="CreatedFrom">Only quotations made at or after this moment.</param>
/// <param name="CreatedUntil">Only quotations made before this moment.</param>
public sealed record QuoteListFilter(
    Guid? CoordinatorId = null,
    Guid? ManagerId = null,
    bool? Open = null,
    bool? Sent = null,
    bool? Accepted = null,
    Guid? InspectorId = null,
    bool? Assigned = null,
    bool CurrentOnly = true,
    string? Search = null,
    int Skip = 0,
    int Take = int.MaxValue,
    IReadOnlyCollection<Inspecta.Domain.Enums.JobStatus>? RequestStatuses = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedUntil = null,
    Guid? ReviewerId = null);

/// <summary>How far one certificate has got, without its form data (which is large): enough to say submitted, approved or delivered.</summary>
public sealed record CertificateState(
    Guid RequestId, Guid? RequestItemId, DateTimeOffset? SubmittedAt, DateTimeOffset? ReviewedAt, string? CertificateNumber, DateTimeOffset? SentAt)
{
    public bool IsApproved => ReviewedAt is not null && CertificateNumber is not null;
    public bool IsDelivered => SentAt is not null;

    public static CertificateState Of(InspectionCertificate certificate) =>
        new(certificate.RequestId, certificate.RequestItemId, certificate.SubmittedAt, certificate.ReviewedAt, certificate.CertificateNumber, certificate.SentAt);
}

public interface IQuoteRepository
{
    Task AddAsync(Quote quote, CancellationToken cancellationToken = default);
    Task<Quote?> GetByIdAsync(Guid quoteId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Quote>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Quotations across requests, newest activity first, each with its request loaded.</summary>
    Task<IReadOnlyList<Quote>> ListAsync(QuoteListFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// The certificate states of the given requests, read without the certificates' form data. A repository whose list already carries the
    /// certificates on each request can leave this empty: the caller then reads them from the request.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<CertificateState>>> ListCertificateStatesAsync(
        IReadOnlyCollection<Guid> requestIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<CertificateState>>>(new Dictionary<Guid, IReadOnlyList<CertificateState>>());
    Task SaveAsync(CancellationToken cancellationToken = default);
}
