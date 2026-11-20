using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

/// <summary>The limits and the retention of the report files of a job.</summary>
public static class JobReportRules
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const int MaxFiles = 20;
    public const long MaxTotalBytes = 30 * 1024 * 1024;

    /// <summary>Reports can be added and removed by their uploader while the inspector works on the job: after the start, before the submit, and again after a return.</summary>
    public static bool IsOpenForUpload(JobStatus status) => status is JobStatus.InProgress or JobStatus.CertificateReturned;

    /// <summary>From approval on the coordinator who owns the job sees the reports, like the certificates.</summary>
    public static bool IsApproved(JobStatus status) => status is JobStatus.CertificateApproved or JobStatus.SentToClient;

    /// <summary>Files that still count: expired ones are treated as gone before the cleanup removes them.</summary>
    public static IEnumerable<JobReportFile> Live(IEnumerable<JobReportFile> files, DateTimeOffset now) => files.Where(file => !file.IsExpired(now));

    /// <summary>The files as ZIP entries under <c>reports/</c>: the cleaned names, made unique (<c>name (2).pdf</c>). Files must be loaded with their bytes.</summary>
    public static IReadOnlyList<(string Name, byte[] Content)> ZipEntries(IEnumerable<JobReportFile> files)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<(string Name, byte[] Content)>();
        foreach (var file in files)
        {
            var name = file.FileName;
            for (var copy = 2; !used.Add(name); copy++)
                name = $"{Path.GetFileNameWithoutExtension(file.FileName)} ({copy}){Path.GetExtension(file.FileName)}";
            entries.Add(($"reports/{name}", file.Content));
        }

        return entries;
    }
}

/// <summary>How long report files are kept after the job is delivered (<c>JobReports:RetentionDays</c>, 30 by default, between 1 and 365).</summary>
public sealed class JobReportOptions(int retentionDays = JobReportOptions.DefaultRetentionDays)
{
    public const int DefaultRetentionDays = 30;

    public int RetentionDays { get; } = Math.Clamp(retentionDays, 1, 365);

    public TimeSpan Retention => TimeSpan.FromDays(RetentionDays);
}
