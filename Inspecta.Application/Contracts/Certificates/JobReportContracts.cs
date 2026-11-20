namespace Inspecta.Application.Contracts.Certificates;

/// <summary>One report file to add to a job: the file name with its extension, the content as base64 and, optionally, the machine it is about.</summary>
public sealed record UploadJobReportRequest
{
    public string? FileName { get; init; }
    public string? ContentBase64 { get; init; }

    /// <summary>A machine of this job; leave empty for a report about the job as a whole.</summary>
    public Guid? RequestItemId { get; init; }
}

/// <summary>One report file of a job. <c>canDelete</c> says whether the signed-in user can remove it now.</summary>
public sealed record JobReportFileResponse(
    Guid Id,
    string FileName,
    string ContentType,
    int SizeBytes,
    Guid? RequestItemId,
    string? MachineName,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    bool CanDelete,
    string DownloadUrl);

/// <summary>
/// The report files of a job, oldest first, with what the screen needs to explain the limits: <c>canUpload</c> (the caller can add one now), the
/// three limits, the total size and <c>expiresAt</c> (empty until the job is delivered; then the day the files are deleted).
/// </summary>
public sealed record JobReportListResponse(
    IReadOnlyList<JobReportFileResponse> Files,
    long TotalBytes,
    bool CanUpload,
    int MaxFiles,
    long MaxFileBytes,
    long MaxTotalBytes,
    DateTimeOffset? ExpiresAt);
