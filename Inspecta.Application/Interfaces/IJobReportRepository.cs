using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

/// <summary>The report files of jobs. Every read carries the team filter of the data layer.</summary>
public interface IJobReportRepository
{
    /// <summary>The job's files without their bytes, oldest first. Expired files are included: the caller decides what counts.</summary>
    Task<IReadOnlyList<JobReportFile>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>How many files the job holds that are not expired at <paramref name="now"/>.</summary>
    Task<int> CountForRequestAsync(Guid requestId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>One file of the job with its bytes, or null when there is none.</summary>
    Task<JobReportFile?> GetWithContentAsync(Guid requestId, Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>The job's files with their bytes, oldest first (for the preview ZIP).</summary>
    Task<IReadOnlyList<JobReportFile>> ListWithContentAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task AddAsync(JobReportFile file, CancellationToken cancellationToken = default);

    /// <summary>Deletes the file and its bytes; false when there was no such file on the job.</summary>
    Task<bool> DeleteAsync(Guid requestId, Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the retention of every file of the job that has none: they are changed in the tracker and saved with the next save of the
    /// job, so the clock starts in the same save as the delivery and never when the delivery fails.
    /// </summary>
    Task StageRetentionAsync(Guid requestId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
}
