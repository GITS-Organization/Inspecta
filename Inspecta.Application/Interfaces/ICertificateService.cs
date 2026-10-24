using Inspecta.Application.Contracts.Certificates;

namespace Inspecta.Application.Interfaces;

public interface ICertificateService
{
    Task<CertificateResponse?> GetAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<CertificateResponse> SaveDraftAsync(Guid requestId, Guid actorId, SaveCertificateDraftRequest request, CancellationToken cancellationToken = default);
    Task<CertificateResponse> SubmitAsync(Guid requestId, Guid actorId, CancellationToken cancellationToken = default);
    Task<CertificateResponse> ReviewAsync(Guid requestId, Guid actorId, ReviewCertificateRequest request, CancellationToken cancellationToken = default);
}