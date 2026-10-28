using Inspecta.Application.Contracts.Certificates;

namespace Inspecta.Application.Interfaces;

public interface ICertificateService
{
    Task<IReadOnlyList<CertificateResponse>?> ListAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<CertificateResponse?> GetAsync(Guid requestId, Guid requestItemId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<CertificateResponse> SaveDraftAsync(Guid requestId, Guid requestItemId, Guid actorId, string role, SaveCertificateDraftRequest request, CancellationToken cancellationToken = default);
    Task<CertificateResponse> SubmitAsync(Guid requestId, Guid requestItemId, Guid actorId, string role, CancellationToken cancellationToken = default);
    Task<CertificateResponse> ReviewAsync(Guid requestId, Guid requestItemId, Guid actorId, ReviewCertificateRequest request, CancellationToken cancellationToken = default);
}