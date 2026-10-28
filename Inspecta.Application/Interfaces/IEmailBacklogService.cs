using Inspecta.Application.Contracts.Mail;

namespace Inspecta.Application.Interfaces;

public interface IEmailBacklogService
{
    Task<CleanupEmailBacklogResponse> CleanupAsync(CleanupEmailBacklogRequest request, CancellationToken cancellationToken = default);
}
