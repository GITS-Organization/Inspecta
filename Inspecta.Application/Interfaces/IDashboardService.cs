using Inspecta.Application.Contracts.Dashboard;

namespace Inspecta.Application.Interfaces;

public interface IDashboardService
{
    /// <exception cref="ArgumentException">The dates are wrong.</exception>
    Task<DashboardOverviewResponse> GetOverviewAsync(
        Guid? managerId, Guid? clientId, Guid? inspectorId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
}
