using Inspecta.Application.Contracts.Mail;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class ClientMailRuleService(
    IClientMailRuleRepository ruleRepository,
    IUserRepository userRepository) : IClientMailRuleService
{
    public async Task<IReadOnlyList<ClientMailRuleResponse>> ListAsync(
        Guid? managerId,
        CancellationToken cancellationToken = default) =>
        (await ruleRepository.ListAsync(managerId, cancellationToken))
            .OrderBy(rule => rule.Pattern, StringComparer.Ordinal)
            .Select(Map)
            .ToArray();

    public async Task<ClientMailRuleResponse> CreateAsync(
        Guid managerId,
        Guid actorId,
        CreateClientMailRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var manager = await userRepository.GetByIdAsync(managerId, cancellationToken);
        if (manager is null || !manager.IsActive || manager.Role != UserRole.TechnicalManager)
            throw new ArgumentException("The selected technical manager is unavailable.", nameof(managerId));

        if (request.ClientId is Guid clientId &&
            await ruleRepository.GetClientManagerIdAsync(clientId, cancellationToken) != managerId)
            throw new AuthException("forbidden", "The client does not belong to this team.", 403);

        var rule = ClientMailRule.Create(managerId, request.Pattern, request.ClientId, actorId);
        if (await ruleRepository.ExistsAsync(managerId, rule.Pattern, cancellationToken))
            throw new InvalidOperationException("This sender is already configured.");

        await ruleRepository.AddAsync(rule, cancellationToken);
        return Map(rule);
    }

    public async Task DeleteAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        var rule = await ruleRepository.GetByIdAsync(ruleId, cancellationToken)
            ?? throw new KeyNotFoundException("Mail rule was not found.");
        await ruleRepository.DeleteAsync(rule, cancellationToken);
    }

    private static ClientMailRuleResponse Map(ClientMailRule rule) =>
        new(rule.Id, rule.ManagerId, rule.Pattern, rule.IsDomain, rule.ClientId, rule.CreatedAt);
}
