using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class ReviewerService(
    IUserRepository users,
    ICompanyRepository companies,
    IRequestRepository requests) : IReviewerService
{
    private const int MaxJobsListed = 20;

    public async Task<Guid?> DefaultReviewerIdAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var company = await companies.GetByIdAsync(companyId, tracking: false, cancellationToken);
        if (company is null) return null;
        if (company.DefaultReviewerId is { } chosen &&
            await users.GetByIdAsync(chosen, cancellationToken) is { Role: UserRole.TechnicalManager, IsActive: true } manager &&
            manager.CompanyId == companyId)
            return chosen;
        return (await ActiveManagersAsync(companyId, cancellationToken)).FirstOrDefault()?.Id;
    }

    public async Task<IReadOnlyList<ReviewerResponse>> ListAsync(
        Guid actorId, string role, Guid? companyId, CancellationToken cancellationToken = default)
    {
        var team = role == "admin" ? companyId : (await users.GetByIdAsync(actorId, cancellationToken))?.CompanyId;
        if (team is null) return [];

        var defaultId = await DefaultReviewerIdAsync(team.Value, cancellationToken);
        return (await ActiveManagersAsync(team.Value, cancellationToken))
            .OrderBy(manager => manager.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(manager => new ReviewerResponse(manager.Id, manager.FullName, manager.Email, manager.Id == defaultId))
            .ToArray();
    }

    public async Task<AppUser> RequireReviewerAsync(Guid companyId, Guid reviewerId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(reviewerId, cancellationToken);
        if (user is null || user.CompanyId != companyId) throw new KeyNotFoundException("Reviewer was not found.");
        if (user.Role != UserRole.TechnicalManager || !user.IsActive)
            throw new AuthException("reviewer_invalid", "The reviewer must be an active technical manager of the company.", 400);
        return user;
    }

    public async Task EnsureCanLeaveAsync(AppUser technicalManager, CancellationToken cancellationToken = default)
    {
        if (technicalManager.Role != UserRole.TechnicalManager) return;
        var waiting = await requests.ListIdsAwaitingReviewerAsync(technicalManager.Id, MaxJobsListed, cancellationToken);
        if (waiting.Count == 0) return;

        throw new AuthException(
            "reviewer_has_pending_reviews",
            "Jobs are waiting for this technical manager's review. Give them to another reviewer first.",
            409)
        {
            Problems = waiting.Select(id => id.ToString()).ToArray()
        };
    }

    public async Task LeftAsync(AppUser technicalManager, CancellationToken cancellationToken = default)
    {
        if (technicalManager.Role != UserRole.TechnicalManager || technicalManager.CompanyId is not { } companyId) return;

        var next = (await ActiveManagersAsync(companyId, cancellationToken)).FirstOrDefault(manager => manager.Id != technicalManager.Id);
        var company = await companies.GetByIdAsync(companyId, tracking: true, cancellationToken);
        if (company is not null && company.DefaultReviewerId == technicalManager.Id)
        {
            company.SetDefaultReviewer(next?.Id);
            await companies.SaveAsync(cancellationToken);
        }

        // The requests not yet started follow the company default (which may be somebody other than the oldest manager).
        var replacement = await DefaultReviewerIdAsync(companyId, cancellationToken);
        await requests.MoveReviewerOfUnstartedRequestsAsync(technicalManager.Id, replacement is { } id && id != technicalManager.Id ? id : next?.Id, cancellationToken);
    }

    public async Task<RequestResponse> ChangeAsync(
        Guid requestId, Guid actorId, string role, ChangeReviewerRequest change, CancellationToken cancellationToken = default)
    {
        var request = await requests.GetByIdAsync(requestId, tracking: true, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role)) throw new KeyNotFoundException("Request was not found.");
        if (role is not ("admin" or "coordinator") || !RequestAccess.CanManageQuotes(request, actorId, role))
            throw new AuthException("forbidden", "Only the coordinator that owns the request can choose its reviewer.", 403);
        var companyId = request.ManagerId ?? throw new InvalidOperationException("The request does not belong to a company.");

        var next = await RequireReviewerAsync(companyId, change.ReviewerId, cancellationToken);
        var previous = request.ReviewerId is { } previousId ? await users.GetByIdAsync(previousId, cancellationToken) : null;
        try
        {
            request.ChangeReviewer(next.Id, actorId, previous?.FullName ?? "no reviewer", next.FullName, change.Note);
        }
        catch (InvalidOperationException exception)
        {
            throw new AuthException("reviewer_not_changeable", exception.Message, 409);
        }

        await requests.SaveAsync(request, cancellationToken);
        return RequestResponseMapper.Map(request, next.FullName);
    }

    // The oldest first: the next default when the current one leaves.
    private async Task<IReadOnlyList<AppUser>> ActiveManagersAsync(Guid companyId, CancellationToken cancellationToken) =>
        (await users.ListAsync(companyId, UserRole.TechnicalManager, isActive: true, cancellationToken))
            .OrderBy(manager => manager.CreatedAt)
            .ToArray();
}
