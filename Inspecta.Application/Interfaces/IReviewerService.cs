using Inspecta.Application.Contracts.Requests;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

/// <summary>
/// The technical manager who reviews a job is chosen per request. Every company has a default (the first technical manager created in it,
/// then the oldest active one if that one leaves); every request starts with it and the coordinator can change it until the job is approved.
/// </summary>
public interface IReviewerService
{
    /// <summary>The reviewer a new request of the company starts with, or null when the company has no active technical manager.</summary>
    Task<Guid?> DefaultReviewerIdAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The technical managers of the caller's company (the platform owner names it), the default marked.</summary>
    Task<IReadOnlyList<ReviewerResponse>> ListAsync(Guid actorId, string role, Guid? companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The user, when they are an active technical manager of the company. <c>404</c> when they do not exist or belong to another company,
    /// <c>400 reviewer_invalid</c> when they are not an active technical manager.
    /// </summary>
    Task<AppUser> RequireReviewerAsync(Guid companyId, Guid reviewerId, CancellationToken cancellationToken = default);

    /// <summary>Refuses (<c>409 reviewer_has_pending_reviews</c>) while jobs are waiting for this technical manager's review.</summary>
    Task EnsureCanLeaveAsync(AppUser technicalManager, CancellationToken cancellationToken = default);

    /// <summary>The technical manager was switched off or deleted: the company default and the requests not yet started move to the next one.</summary>
    Task LeftAsync(AppUser technicalManager, CancellationToken cancellationToken = default);

    /// <summary>Gives the request to another reviewer (the coordinator that owns it, or the platform owner), until it is approved.</summary>
    Task<RequestResponse> ChangeAsync(Guid requestId, Guid actorId, string role, ChangeReviewerRequest change, CancellationToken cancellationToken = default);
}
