using Inspecta.Application.Contracts.Quotations;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

public sealed class QuotationDraftService(IQuotationDraftRepository draftRepository) : IQuotationDraftService
{
    public async Task<IReadOnlyList<QuotationDraftResponse>> ListAsync(
        Guid coordinatorId,
        CancellationToken cancellationToken = default) =>
        (await draftRepository.ListForCoordinatorAsync(coordinatorId, cancellationToken))
        .Select(Map)
        .ToArray();

    public async Task<QuotationDraftResponse?> UpdateAsync(
        Guid coordinatorId,
        Guid draftId,
        UpdateQuotationDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var draft = await draftRepository.GetForCoordinatorAsync(
            coordinatorId,
            draftId,
            tracking: true,
            cancellationToken);
        if (draft is null)
            return null;

        draft.UpdateDetails(request.ClientName, request.Location, request.MachineDetails);
        await draftRepository.SaveAsync(cancellationToken);
        return Map(draft);
    }

    private static QuotationDraftResponse Map(QuotationDraft draft) => new(
        draft.Id,
        draft.CoordinatorId,
        draft.ClientName,
        draft.Location,
        draft.MachineDetails,
        draft.RawEmailBody,
        draft.SenderEmail,
        draft.EmailSubject,
        draft.Status,
        draft.CreatedAt);
}
