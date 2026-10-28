using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface ICertificateRepository
{
    Task<IReadOnlyList<InspectionCertificate>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<InspectionCertificate?> GetByRequestItemIdAsync(Guid requestId, Guid requestItemId, bool tracking, CancellationToken cancellationToken = default);
    Task AddAsync(InspectionCertificate certificate, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}