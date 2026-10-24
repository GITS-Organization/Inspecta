using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface ICertificateRepository
{
    Task<InspectionCertificate?> GetByRequestIdAsync(Guid requestId, bool tracking, CancellationToken cancellationToken = default);
    Task AddAsync(InspectionCertificate certificate, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}