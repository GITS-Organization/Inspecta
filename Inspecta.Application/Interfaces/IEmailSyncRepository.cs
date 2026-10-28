using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IEmailSyncRepository
{
    Task<IReadOnlyList<CoordinatorEmailConfig>> ListActiveConfigsAsync(
        CancellationToken cancellationToken = default);

    Task<bool> SaveDraftIfNewAsync(
        QuotationDraft draft,
        CancellationToken cancellationToken = default);
}
