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
    private async Task EnsureTeamCoordinatorAsync(Guid managerId, Guid coordinatorId, CancellationToken cancellationToken)
    {
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
    }

    private static CoordinatorEmailConfigResponse Map(CoordinatorEmailConfig? config) => config is null
        ? new CoordinatorEmailConfigResponse(false, null, null, null, null, null, null, null, CoordinatorEmailConfig.DefaultSyncFromUtc, null)
        : new CoordinatorEmailConfigResponse(
            true, config.ImapServer, config.ImapPort, config.EmailUsername, config.UseSsl,
            config.SmtpServer, config.SmtpPort, config.SmtpUseSsl, config.SyncFromUtc, config.UpdatedAt,
            config.AuthMode == MailboxAuthMode.MicrosoftOAuth ? "microsoft_oauth" : "password",
            config.ConnectionStatus == MailboxConnectionStatus.NeedsReconnect ? "needs_reconnect" : "connected",
            config.LastAuthError);

    public async Task<CoordinatorEmailConfigResponse> GetAsync(Guid managerId, Guid coordinatorId, CancellationToken cancellationToken = default)
    {
        await EnsureTeamCoordinatorAsync(managerId, coordinatorId, cancellationToken);
        return Map(await configRepository.GetByCoordinatorAsync(coordinatorId, cancellationToken));
    }

    public async Task<CoordinatorEmailConfigResponse> GetOwnAsync(Guid coordinatorId, CancellationToken cancellationToken = default)
    {
        var coordinator = await userRepository.GetByIdAsync(coordinatorId, cancellationToken);
        if (coordinator is not { Role: UserRole.Coordinator, ManagerId: Guid managerId })
            throw new AuthException("coordinator_not_found", "Coordinator was not found.", 404);
        return await GetAsync(managerId, coordinatorId, cancellationToken);
    }

    public async Task DeleteAsync(Guid managerId, Guid coordinatorId, CancellationToken cancellationToken = default)
    {
        await EnsureTeamCoordinatorAsync(managerId, coordinatorId, cancellationToken);
        var config = await configRepository.GetByCoordinatorAsync(coordinatorId, cancellationToken)
            ?? throw new AuthException("email_config_not_found", "This coordinator has no mailbox set up.", 404);
        await configRepository.DeleteAsync(config, cancellationToken);
    }

    public async Task<CoordinatorEmailConfigResponse> ChangeSyncFromAsync(
        Guid managerId, Guid coordinatorId, DateTimeOffset syncFrom, CancellationToken cancellationToken = default)
    {
        await EnsureTeamCoordinatorAsync(managerId, coordinatorId, cancellationToken);
        var config = await configRepository.GetByCoordinatorAsync(coordinatorId, cancellationToken)
            ?? throw new AuthException("email_config_not_found", "Set the mailbox first, then choose the date to import from.", 404);
        config.ChangeSyncFrom(syncFrom);
        await configRepository.SaveAsync(config, cancellationToken);
        return Map(config);
    }

    public async Task UpdateAsync(
        Guid managerId,
        Guid coordinatorId,
        CoordinatorEmailConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        await EnsureTeamCoordinatorAsync(managerId, coordinatorId, cancellationToken);

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
                request.UseSsl,
                smtpServer: request.SmtpServer,
                smtpPort: request.SmtpPort,
                smtpUseSsl: request.SmtpUseSsl,
                syncFromUtc: request.SyncFrom ?? CoordinatorEmailConfig.DefaultSyncFromUtc);
        }
        else
        {
            config.Update(
                request.ImapServer,
                request.ImapPort,
                request.EmailUsername,
                encryptedPassword,
                request.UseSsl,
                smtpServer: request.SmtpServer,
                smtpPort: request.SmtpPort,
                smtpUseSsl: request.SmtpUseSsl,
                syncFromUtc: request.SyncFrom);
        }

        await configRepository.SaveAsync(config, cancellationToken);
    }

    private static void Validate(CoordinatorEmailConfigRequest request)
    {
        if (request.SyncFrom is { } from && (from < CoordinatorEmailConfig.EarliestSyncFromUtc || from > DateTimeOffset.UtcNow.AddDays(30)))
            throw new ArgumentException("Choose a date from 2020 up to 30 days ahead.");
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
