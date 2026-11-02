using Inspecta.Application.Contracts.Requests;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Interfaces;

/// <summary>What the dashboard counts: a team (<c>ManagerId</c>; empty for every team), and optionally one client and one inspector.</summary>
public sealed record DashboardFilter(Guid? ManagerId, Guid? ClientId, Guid? InspectorId);

public sealed record ApprovedCertificateRow(Guid InspectorId, DateTimeOffset ApprovedAt, CertificateResult? Result);

/// <summary>A job assigned to an inspector that they carried out, and the day it was scheduled for.</summary>
public sealed record WorkedJobRow(Guid InspectorId, DateTimeOffset ScheduledAt);

public sealed record DueEquipmentRow(Guid Id, string Name, string? SerialNumber, Guid ClientId, string ClientName, DateTimeOffset DueAt);

public interface IDashboardRepository
{
    /// <summary>The real requests (not the maybe emails) of the filter, counted by status.</summary>
    Task<IReadOnlyList<RequestStatusCount>> CountRequestsByStatusAsync(DashboardFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Certificates approved between the two moments, with who made them and the verdict.</summary>
    Task<IReadOnlyList<ApprovedCertificateRow>> ListApprovedCertificatesAsync(
        DashboardFilter filter, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>
    /// The jobs scheduled between the two moments that were assigned to an inspector and carried out (their certificates were submitted:
    /// <c>certificate_submitted</c>, <c>certificate_returned</c>, <c>certificate_approved</c> or <c>sent_to_client</c>).
    /// </summary>
    Task<IReadOnlyList<WorkedJobRow>> ListWorkedJobsAsync(
        DashboardFilter filter, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>Active items (of clients that are not archived) whose next inspection is due on or before the cutoff. The inspector filter does not apply.</summary>
    Task<IReadOnlyList<DueEquipmentRow>> ListEquipmentDueAsync(DashboardFilter filter, DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}
