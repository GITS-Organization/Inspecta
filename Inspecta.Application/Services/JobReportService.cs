using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

/// <summary>
/// Report files on a job. Access always starts with the job itself (a job the caller cannot see answers 404, like everywhere else); then
/// the role and the status of the job decide: the inspector of the job, the technical manager and the admin upload while the inspector works
/// (<see cref="JobReportRules.IsOpenForUpload"/>); the inspector, technical managers and admin read them; the coordinator who owns the job reads
/// them from approval; deleting is the uploader's until the inspector submits and the reviewer's (or admin's) any time until the files expire.
/// </summary>
public sealed class JobReportService(
    IRequestRepository requestRepository,
    IJobReportRepository reports,
    IUserRepository userRepository) : IJobReportService
{
    public async Task<JobReportListResponse> ListAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, cancellationToken);
        EnsureCanRead(request, actorId, role);
        return await BuildListAsync(request, actorId, role, cancellationToken);
    }

    public async Task<JobReportFileResponse> UploadAsync(
        Guid requestId, Guid actorId, string role, UploadJobReportRequest upload, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, cancellationToken);
        if (!CanUpload(request, actorId, role))
            throw new AuthException("forbidden", "Only the assigned inspector or a technical manager can add a report to this job.", 403);
        if (!JobReportRules.IsOpenForUpload(request.Status))
            throw new AuthException("reports_locked", "Reports can be added while the inspection is in progress, before the certificates are submitted.", 409);

        var (name, contentType, content) = Decode(upload);
        if (upload.RequestItemId is Guid itemId && request.Items.All(item => item.Id != itemId))
            throw new ArgumentException("That machine is not part of this job.");

        var now = DateTimeOffset.UtcNow;
        var existing = JobReportRules.Live(await reports.ListForRequestAsync(request.Id, cancellationToken), now).ToList();
        if (existing.Count >= JobReportRules.MaxFiles)
            throw new AuthException("report_limit_reached", $"A job can hold {JobReportRules.MaxFiles} report files at most. Remove one first.", 409);
        if (existing.Sum(file => (long)file.ContentLength) + content.Length > JobReportRules.MaxTotalBytes)
            throw new AuthException("report_limit_reached", $"The reports of a job can be {JobReportRules.MaxTotalBytes / (1024 * 1024)} MB together at most. Remove a file or send a smaller one.", 409);

        var uploader = await userRepository.GetByIdAsync(actorId, cancellationToken);
        var file = JobReportFile.Create(
            request.ManagerId ?? Guid.Empty, request.Id, upload.RequestItemId, actorId, uploader?.FullName ?? string.Empty, name, contentType, content, now);
        await reports.AddAsync(file, cancellationToken);
        return ToResponse(file, request, actorId, role);
    }

    public async Task<DownloadFile> DownloadAsync(
        Guid requestId, Guid fileId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, cancellationToken);
        EnsureCanRead(request, actorId, role);
        var file = await reports.GetWithContentAsync(request.Id, fileId, cancellationToken);
        if (file is null || file.IsExpired(DateTimeOffset.UtcNow)) throw new KeyNotFoundException("Report was not found.");
        return new DownloadFile(file.Content, file.FileName, file.ContentType);
    }

    public async Task<JobReportListResponse> DeleteAsync(
        Guid requestId, Guid fileId, Guid actorId, string role, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(requestId, actorId, role, cancellationToken);
        var file = JobReportRules.Live(await reports.ListForRequestAsync(request.Id, cancellationToken), DateTimeOffset.UtcNow)
            .SingleOrDefault(value => value.Id == fileId);
        if (role == "coordinator")
            throw new AuthException("forbidden", "A coordinator cannot remove reports.", 403);
        if (file is null) throw new KeyNotFoundException("Report was not found.");

        if (!IsTheReviewerOf(request, actorId, role))
        {
            if (file.UploadedById != actorId)
                throw role == "technical_manager"
                    ? new AuthException("not_the_reviewer", "This job was given to another technical manager for review.", 403)
                    : new AuthException("forbidden", "You can only remove the reports you added.", 403);
            if (!JobReportRules.IsOpenForUpload(request.Status))
                throw new AuthException("reports_locked", "The certificates were submitted: the technical manager decides about the reports now.", 409);
        }

        await reports.DeleteAsync(request.Id, file.Id, cancellationToken);
        return await BuildListAsync(request, actorId, role, cancellationToken);
    }

    // ---- Rules ---------------------------------------------------------------------------------------------------------

    private async Task<InspectionRequest> LoadAsync(Guid requestId, Guid actorId, string role, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(requestId, tracking: false, cancellationToken);
        if (request is null || !RequestAccess.IsVisible(request, actorId, role))
            throw new KeyNotFoundException("Request was not found.");
        return request;
    }

    // The assigned inspector, any technical manager of the company and the admin do the inspection work.
    private static bool CanUpload(InspectionRequest request, Guid actorId, string role) =>
        role is "admin" or "technical_manager" || role == "inspector" && request.InspectorId == actorId;

    private static bool IsTheReviewerOf(InspectionRequest request, Guid actorId, string role) =>
        role == "admin" || role == "technical_manager" && request.ReviewerId == actorId;

    private static bool CanSee(InspectionRequest request, Guid actorId, string role) => role switch
    {
        "coordinator" => RequestAccess.CanManageQuotes(request, actorId, role) && JobReportRules.IsApproved(request.Status),
        _ => true
    };

    private static void EnsureCanRead(InspectionRequest request, Guid actorId, string role)
    {
        if (role == "coordinator" && !RequestAccess.CanManageQuotes(request, actorId, role))
            throw new AuthException("forbidden", "Only the coordinator that owns the request can see its reports.", 403);
        if (!CanSee(request, actorId, role))
            throw new AuthException("not_approved", "The reports are available to the coordinator once the certificates are approved.", 409);
    }

    private static (string Name, string ContentType, byte[] Content) Decode(UploadJobReportRequest upload)
    {
        var name = UploadedFileRules.CleanName(upload.FileName)
            ?? throw new ArgumentException("A report needs a name with an extension, at most 150 characters.");
        var extension = Path.GetExtension(name);
        if (!UploadedFileRules.TryGetContentType(extension, allowZip: false, out var contentType))
            throw new ArgumentException($"'{name}' cannot be added: this type of file is not allowed (a ZIP is not accepted).");

        // Refuse a file that is too big before decoding it: base64 is 4 characters for every 3 bytes.
        var text = upload.ContentBase64 ?? string.Empty;
        if (text.Length > (JobReportRules.MaxFileBytes / 3 + 2) * 4)
            throw new ArgumentException($"'{name}' is larger than {JobReportRules.MaxFileBytes / (1024 * 1024)} MB.");
        byte[] content;
        try { content = Convert.FromBase64String(text); }
        catch (FormatException) { throw new ArgumentException($"'{name}' is not valid base64."); }
        if (content.Length == 0) throw new ArgumentException($"'{name}' is empty.");
        if (content.Length > JobReportRules.MaxFileBytes)
            throw new ArgumentException($"'{name}' is larger than {JobReportRules.MaxFileBytes / (1024 * 1024)} MB.");
        if (UploadedFileRules.LooksLikeProgram(content))
            throw new ArgumentException($"'{name}' looks like a program, which cannot be added.");
        if (!UploadedFileRules.MatchesType(extension, content))
            throw new ArgumentException($"'{name}' is not what its extension says.");
        return (name, contentType, content);
    }

    // ---- Answers -------------------------------------------------------------------------------------------------------

    private async Task<JobReportListResponse> BuildListAsync(InspectionRequest request, Guid actorId, string role, CancellationToken cancellationToken)
    {
        var files = JobReportRules.Live(await reports.ListForRequestAsync(request.Id, cancellationToken), DateTimeOffset.UtcNow)
            .OrderBy(file => file.CreatedAt.UtcDateTime).ThenBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new JobReportListResponse(
            files.Select(file => ToResponse(file, request, actorId, role)).ToArray(),
            files.Sum(file => (long)file.ContentLength),
            CanUpload(request, actorId, role) && JobReportRules.IsOpenForUpload(request.Status),
            JobReportRules.MaxFiles,
            JobReportRules.MaxFileBytes,
            JobReportRules.MaxTotalBytes,
            files.Where(file => file.ExpiresAt is not null).Select(file => file.ExpiresAt).Min());
    }

    private static JobReportFileResponse ToResponse(JobReportFile file, InspectionRequest request, Guid actorId, string role) =>
        new(file.Id,
            file.FileName,
            file.ContentType,
            file.ContentLength,
            file.RequestItemId,
            request.Items.FirstOrDefault(item => item.Id == file.RequestItemId)?.Name,
            file.UploadedByName,
            file.CreatedAt,
            CanDelete(file, request, actorId, role),
            $"/api/v1/quotation-queue/{request.Id}/reports/{file.Id}");

    private static bool CanDelete(JobReportFile file, InspectionRequest request, Guid actorId, string role) =>
        role != "coordinator" &&
        (IsTheReviewerOf(request, actorId, role) || file.UploadedById == actorId && JobReportRules.IsOpenForUpload(request.Status));
}
