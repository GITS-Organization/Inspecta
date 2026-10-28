using Inspecta.Application.Contracts.Mail;
using Inspecta.Application.Interfaces;

namespace Inspecta.Application.Services;

public sealed class EmailBacklogService(IEmailSyncRepository repository) : IEmailBacklogService
{
    public async Task<CleanupEmailBacklogResponse> CleanupAsync(
        CleanupEmailBacklogRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CreatedBefore > DateTimeOffset.UtcNow)
            throw new ArgumentException("createdBefore cannot be in the future.", nameof(request));
        if (request.ManagerId == Guid.Empty)
            throw new ArgumentException("managerId cannot be empty.", nameof(request));

        var matched = await repository.CountUntouchedEmailRequestsAsync(request.CreatedBefore, request.ManagerId, cancellationToken);
        if (request.DryRun || matched == 0)
            return new CleanupEmailBacklogResponse(matched, 0, request.DryRun);

        var deleted = await repository.DeleteUntouchedEmailRequestsAsync(request.CreatedBefore, request.ManagerId, cancellationToken);
        return new CleanupEmailBacklogResponse(matched, deleted, false);
    }
}
