using Inspecta.Application.Contracts.Executive;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

/// <summary>The read-only answers for the general manager's screens, and the company targets they are measured against.</summary>
public sealed class ExecutiveService(IExecutiveTargetsRepository targetsRepository) : IExecutiveService
{
    public async Task<ExecutiveTargetsResponse> GetTargetsAsync(Guid? managerId, CancellationToken cancellationToken = default)
    {
        var stored = managerId is Guid id ? await targetsRepository.GetAsync(id, false, cancellationToken) : null;
        return Map(stored);
    }

    public async Task<ExecutiveTargetsResponse> SaveTargetsAsync(
        Guid managerId, SaveExecutiveTargetsRequest request, Guid actorId, CancellationToken cancellationToken = default)
    {
        var targets = await targetsRepository.GetAsync(managerId, true, cancellationToken);
        if (targets is null)
        {
            targets = ExecutiveTargets.Create(
                managerId, request.CollectionRatePercent, request.DaysToCollect, request.RequestToDeliveryDays, request.ReviewDaysPerStep, actorId);
            await targetsRepository.AddAsync(targets, cancellationToken);
        }
        else
        {
            targets.Update(request.CollectionRatePercent, request.DaysToCollect, request.RequestToDeliveryDays, request.ReviewDaysPerStep, actorId);
            await targetsRepository.SaveAsync(cancellationToken);
        }

        return Map(targets);
    }

    internal static ExecutiveTargetsResponse Map(ExecutiveTargets? targets) => targets is null
        ? new ExecutiveTargetsResponse(
            ExecutiveTargets.DefaultCollectionRatePercent, ExecutiveTargets.DefaultDaysToCollect,
            ExecutiveTargets.DefaultRequestToDeliveryDays, ExecutiveTargets.DefaultReviewDaysPerStep)
        : new ExecutiveTargetsResponse(targets.CollectionRatePercent, targets.DaysToCollect, targets.RequestToDeliveryDays, targets.ReviewDaysPerStep);
}
