using System.Security.Cryptography;

namespace Inspecta.Domain.Entities;

/// <summary>
/// A report file an inspector (or the technical manager) attached to a job: a scanned report, photos, a spreadsheet. It belongs to the job, with an
/// optional link to one machine of it. The bytes live in their own table (<see cref="JobReportContent"/>) so lists never read them. Never part of a
/// printed certificate and never reachable from the public check page. <see cref="ExpiresAt"/> stays empty until the job is delivered; after it the
/// file is treated as gone and deleted.
/// </summary>
public sealed class JobReportFile
{
    public const int MaxFileNameLength = 150;
    public const int MaxContentTypeLength = 100;
    public const int MaxUploaderNameLength = 200;

    private JobReportFile() { }

    public Guid Id { get; private set; }
    public Guid ManagerId { get; private set; }
    public Guid RequestId { get; private set; }

    /// <summary>The machine the file is about; empty for the job as a whole, and emptied when that machine is taken out of the job.</summary>
    public Guid? RequestItemId { get; private set; }
    public Guid UploadedById { get; private set; }
    public string UploadedByName { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public int ContentLength { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the file is deleted: set at delivery (delivery date plus the retention), empty before.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public JobReportContent? Body { get; private set; }

    /// <summary>The file's bytes. Load the file with its body (<c>Include</c>) first; <see cref="ContentLength"/> is always available.</summary>
    public byte[] Content => Body?.Bytes ?? throw new InvalidOperationException("The report file was loaded without its content.");

    /// <summary>True once the retention has run out: such a file is treated as gone even before the cleanup removes it.</summary>
    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expires && expires <= now;

    /// <summary>Starts the retention clock. A file that already has an expiry keeps it.</summary>
    public void StartRetention(DateTimeOffset expiresAt) => ExpiresAt ??= expiresAt;

    public static JobReportFile Create(
        Guid managerId,
        Guid requestId,
        Guid? requestItemId,
        Guid uploadedById,
        string uploadedByName,
        string fileName,
        string contentType,
        byte[] content,
        DateTimeOffset? createdAt = null)
    {
        if (requestId == Guid.Empty || uploadedById == Guid.Empty) throw new ArgumentException("The job and the uploader are required.");
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > MaxFileNameLength) throw new ArgumentException("A report needs a name of at most 150 characters.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentException("A report needs a content type.", nameof(contentType));
        if (content is null || content.Length == 0) throw new ArgumentException("The report file is empty.", nameof(content));

        var id = Guid.NewGuid();
        var uploader = (uploadedByName ?? string.Empty).Trim();
        return new JobReportFile
        {
            Id = id,
            ManagerId = managerId,
            RequestId = requestId,
            RequestItemId = requestItemId,
            UploadedById = uploadedById,
            UploadedByName = uploader.Length > MaxUploaderNameLength ? uploader[..MaxUploaderNameLength] : uploader,
            FileName = fileName,
            ContentType = contentType,
            ContentLength = content.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            Body = new JobReportContent(id, content)
        };
    }
}
