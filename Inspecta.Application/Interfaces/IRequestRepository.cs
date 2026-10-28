using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IRequestRepository
{
    Task AddAsync(InspectionRequest request, CancellationToken cancellationToken = default);
    Task SaveAsync(InspectionRequest request, CancellationToken cancellationToken = default);
    Task<InspectionRequest?> GetByIdAsync(Guid requestId, bool tracking, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InspectionRequest>> ListAsync(Guid? inspectorId, CancellationToken cancellationToken = default);
    Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<Guid?> GetClientManagerIdAsync(Guid clientId, CancellationToken cancellationToken = default);
}