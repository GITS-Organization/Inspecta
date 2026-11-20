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
        var list = files.ToList();
        var names = UniqueNames(list);
        return list.Select((file, index) => ($"reports/{names[index]}", file.Content)).ToArray();
    }

    /// <summary>The names the files go by in a ZIP and in the email, in the same order: the cleaned names, made unique (<c>name (2).pdf</c>).</summary>
    public static IReadOnlyList<string> UniqueNames(IEnumerable<JobReportFile> files)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();
        foreach (var file in files)
        {
            var name = file.FileName;
            for (var copy = 2; !used.Add(name); copy++)
                name = $"{Path.GetFileNameWithoutExtension(file.FileName)} ({copy}){Path.GetExtension(file.FileName)}";
            names.Add(name);
        }

        return names;
    }
}

/// <summary>
/// How long report files are kept after the job is delivered (<c>JobReports:RetentionDays</c>, 30 by default, between 1 and 365) and how big the
/// certificates email may be when it carries reports (<c>JobReports:MaxEmailBytes</c>, 17 MB raw by default, between 1 MB and 25 MB).
/// </summary>
public sealed class JobReportOptions(int retentionDays = JobReportOptions.DefaultRetentionDays, long maxEmailBytes = JobReportOptions.DefaultMaxEmailBytes)
{
    public const int DefaultRetentionDays = 30;
    public const long DefaultMaxEmailBytes = 17 * 1024 * 1024; // raw bytes: base64 makes the mail about a third bigger, so 17 MB stays under a 25 MB server limit

    public int RetentionDays { get; } = Math.Clamp(retentionDays, 1, 365);

    /// <summary>The most the attachments of one certificates email may total when at least one report goes with it.</summary>
    public long MaxEmailBytes { get; } = Math.Clamp(maxEmailBytes, 1024 * 1024, 25 * 1024 * 1024);

    public TimeSpan Retention => TimeSpan.FromDays(RetentionDays);
}
