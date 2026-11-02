using Inspecta.Application.Contracts.Requests;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Interfaces;

/// <summary>What the dashboard counts: a team (<c>ManagerId</c>; empty for every team), and optionally one client and one inspector.</summary>
public sealed record DashboardFilter(Guid? ManagerId, Guid? ClientId, Guid? InspectorId);

public sealed record ApprovedCertificateRow(Guid InspectorId, DateTimeOffset ApprovedAt, CertificateResult? Result);

public sealed record DueEquipmentRow(Guid Id, string Name, string? SerialNumber, Guid ClientId, string ClientName, DateTimeOffset DueAt);

public interface IDashboardRepository
{
    /// <summary>The real requests (not the maybe emails) of the filter, counted by status.</summary>
    Task<IReadOnlyList<RequestStatusCount>> CountRequestsByStatusAsync(DashboardFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Certificates approved between the two moments, with who made them and the verdict.</summary>
    Task<IReadOnlyList<ApprovedCertificateRow>> ListApprovedCertificatesAsync(
        DashboardFilter filter, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>Active items (of clients that are not archived) whose next inspection is due on or before the cutoff. The inspector filter does not apply.</summary>
    Task<IReadOnlyList<DueEquipmentRow>> ListEquipmentDueAsync(DashboardFilter filter, DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}
