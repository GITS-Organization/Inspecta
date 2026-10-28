using Inspecta.Application.Contracts.Quotations;

namespace Inspecta.Application.Interfaces;

public interface IQuotationDraftService
{
    Task<IReadOnlyList<QuotationDraftResponse>> ListAsync(
        Guid coordinatorId,
        CancellationToken cancellationToken = default);

    Task<QuotationDraftResponse?> UpdateAsync(
        Guid coordinatorId,
        Guid draftId,
        UpdateQuotationDraftRequest request,
        CancellationToken cancellationToken = default);
}
