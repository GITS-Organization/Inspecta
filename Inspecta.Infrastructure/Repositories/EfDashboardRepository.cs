using Inspecta.Application.Contracts.Requests;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

/// <summary>Counts for the manager's overview. Every figure is a query that counts or lists small rows; nothing loads whole entities.</summary>
public sealed class EfDashboardRepository(InspectaDbContext dbContext) : IDashboardRepository
{
    public async Task<IReadOnlyList<RequestStatusCount>> CountRequestsByStatusAsync(
        DashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var requests = dbContext.Requests.AsNoTracking().Where(request => request.Triage == RequestTriage.Request);
        if (filter.ManagerId is Guid managerId) requests = requests.Where(request => request.ManagerId == managerId);
        if (filter.ClientId is Guid clientId) requests = requests.Where(request => request.ClientId == clientId);
        if (filter.InspectorId is Guid inspectorId) requests = requests.Where(request => request.InspectorId == inspectorId);

        var counts = await requests
            .GroupBy(request => request.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        return counts.Select(item => new RequestStatusCount(item.Status, item.Count)).ToArray();
    }

    public async Task<IReadOnlyList<ApprovedCertificateRow>> ListApprovedCertificatesAsync(
        DashboardFilter filter, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        // Approved: reviewed and numbered. The request carries the team and the client.
        var certificates = dbContext.Certificates.AsNoTracking()
            .Where(certificate => certificate.ReviewedAt != null && certificate.CertificateNumber != null &&
                                  certificate.ReviewedAt >= from && certificate.ReviewedAt <= to);
        if (filter.ManagerId is Guid managerId) certificates = certificates.Where(certificate => certificate.Request!.ManagerId == managerId);
        if (filter.ClientId is Guid clientId) certificates = certificates.Where(certificate => certificate.Request!.ClientId == clientId);
        if (filter.InspectorId is Guid inspectorId) certificates = certificates.Where(certificate => certificate.InspectorId == inspectorId);

        return await certificates
            .Select(certificate => new ApprovedCertificateRow(certificate.InspectorId, certificate.ReviewedAt!.Value, certificate.FinalResult))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkedJobRow>> ListWorkedJobsAsync(
        DashboardFilter filter, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        JobStatus[] carriedOut = [JobStatus.CertificateSubmitted, JobStatus.CertificateReturned, JobStatus.CertificateApproved, JobStatus.SentToClient];
        var jobs = dbContext.Requests.AsNoTracking()
            .Where(request => request.Triage == RequestTriage.Request && request.InspectorId != null && request.ScheduledAt != null &&
                              request.ScheduledAt >= from && request.ScheduledAt <= to && carriedOut.Contains(request.Status));
        if (filter.ManagerId is Guid managerId) jobs = jobs.Where(request => request.ManagerId == managerId);
        if (filter.ClientId is Guid clientId) jobs = jobs.Where(request => request.ClientId == clientId);
        if (filter.InspectorId is Guid inspectorId) jobs = jobs.Where(request => request.InspectorId == inspectorId);

        return await jobs
            .Select(request => new WorkedJobRow(request.InspectorId!.Value, request.ScheduledAt!.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DueEquipmentRow>> ListEquipmentDueAsync(
        DashboardFilter filter, DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        var items = dbContext.Items.AsNoTracking()
            .Where(item => item.NextInspectionDueAt != null && item.NextInspectionDueAt <= cutoff &&
                           item.Status == EquipmentStatus.Active && !item.IsArchived && item.ArchivedAt == null &&
                           item.Client!.ArchivedAt == null);
        if (filter.ManagerId is Guid managerId) items = items.Where(item => item.Client!.ManagerId == managerId);
        if (filter.ClientId is Guid clientId) items = items.Where(item => item.ClientId == clientId);

        return await items
            .Select(item => new DueEquipmentRow(item.Id, item.Name, item.SerialNumber, item.ClientId, item.Client!.Name, item.NextInspectionDueAt!.Value))
            .ToListAsync(cancellationToken);
    }
}
