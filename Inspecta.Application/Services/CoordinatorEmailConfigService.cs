using Inspecta.Application.Contracts.Email;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class CoordinatorEmailConfigService(
    IUserRepository userRepository,
    ICoordinatorEmailConfigRepository configRepository,
    ISecretProtector secretProtector) : ICoordinatorEmailConfigService
{
    public async Task UpdateAsync(
        Guid managerId,
        Guid coordinatorId,
        CoordinatorEmailConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var manager = await userRepository.GetByIdAsync(managerId, cancellationToken);
        if (manager is null || manager.Role != UserRole.TechnicalManager || !manager.HasActiveSubscription())
            throw new AuthException("manager_not_found", "Active technical manager was not found.", 404);

        var coordinator = await userRepository.GetByIdAsync(coordinatorId, cancellationToken);
        if (coordinator is null ||
            coordinator.Role != UserRole.Coordinator ||
            coordinator.ManagerId != managerId)
        {
            throw new AuthException("coordinator_not_found", "Coordinator was not found in this team.", 404);
        }

        var encryptedPassword = secretProtector.Protect(request.Password);
        var config = await configRepository.GetByCoordinatorAsync(coordinatorId, cancellationToken);
        if (config is null)
        {
            config = CoordinatorEmailConfig.Create(
                coordinatorId,
                managerId,
                request.ImapServer,
                request.ImapPort,
                request.EmailUsername,
                encryptedPassword,
                request.UseSsl);
        }
        else
        {
            config.Update(
                request.ImapServer,
                request.ImapPort,
                request.EmailUsername,
                encryptedPassword,
                request.UseSsl);
        }

        await configRepository.SaveAsync(config, cancellationToken);
    }

    private static void Validate(CoordinatorEmailConfigRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ImapServer) ||
            request.ImapServer.Length > 255 ||
            request.ImapPort is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(request.EmailUsername) ||
            request.EmailUsername.Length > 320 ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Password.Length > 256)
        {
            throw new ArgumentException("IMAP server, valid port, username, and password are required.");
        }
    }
}
