using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IQuotationDraftRepository
{
    Task<IReadOnlyList<QuotationDraft>> ListForCoordinatorAsync(
        Guid coordinatorId,
        CancellationToken cancellationToken = default);

    Task<QuotationDraft?> GetForCoordinatorAsync(
        Guid coordinatorId,
        Guid draftId,
        bool tracking,
        CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}
