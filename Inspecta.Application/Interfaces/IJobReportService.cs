using Inspecta.Application.Contracts.Certificates;

namespace Inspecta.Application.Interfaces;

/// <summary>Report files the inspector attaches to a job, and the technical manager's review of them.</summary>
public interface IJobReportService
{
    Task<JobReportListResponse> ListAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<JobReportFileResponse> UploadAsync(Guid requestId, Guid actorId, string role, UploadJobReportRequest upload, CancellationToken cancellationToken = default);

    Task<DownloadFile> DownloadAsync(Guid requestId, Guid fileId, Guid actorId, string role, CancellationToken cancellationToken = default);

    Task<JobReportListResponse> DeleteAsync(Guid requestId, Guid fileId, Guid actorId, string role, CancellationToken cancellationToken = default);
}
