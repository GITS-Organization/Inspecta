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

        // Mail from this sender that was waiting as a "maybe" now belongs to the client: it becomes a real request.
        var promoted = 0;
        if (rule.ClientId is Guid linkedClient &&
            await ruleRepository.GetClientSummaryAsync(linkedClient, cancellationToken) is { } client)
            promoted = await ruleRepository.PromoteMaybesAsync(rule, client, cancellationToken);
        return Map(rule) with { PromotedRequests = promoted };
    }

    public async Task<IReadOnlyList<ClientMailRuleResponse>> ListForClientAsync(
        Guid clientId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        await EnsureClientAsync(clientId, tenantId, cancellationToken);
        return (await ruleRepository.ListAsync(tenantId, cancellationToken))
            .Where(rule => rule.ClientId == clientId)
            .OrderBy(rule => rule.Pattern, StringComparer.Ordinal)
            .Select(Map)
            .ToArray();
    }

    public async Task<ClientMailRuleResponse> AddForClientAsync(
        Guid clientId,
        Guid? tenantId,
        Guid actorId,
        AddClientEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var managerId = await EnsureClientAsync(clientId, tenantId, cancellationToken);
        return await CreateAsync(
            managerId, actorId, new CreateClientMailRuleRequest { Pattern = request.Pattern, ClientId = clientId }, cancellationToken);
    }

    public async Task RemoveForClientAsync(
        Guid clientId,
        Guid ruleId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        await EnsureClientAsync(clientId, tenantId, cancellationToken);
        var rule = await ruleRepository.GetByIdAsync(ruleId, cancellationToken);
        if (rule is null || rule.ClientId != clientId)
            throw new KeyNotFoundException("Mail rule was not found.");
        await ruleRepository.DeleteAsync(rule, cancellationToken);
    }

    /// <summary>The team of the client; a caller that belongs to a team (not Admin) can only reach their own team's clients.</summary>
    private async Task<Guid> EnsureClientAsync(Guid clientId, Guid? tenantId, CancellationToken cancellationToken)
    {
        var managerId = await ruleRepository.GetClientManagerIdAsync(clientId, cancellationToken)
            ?? throw new KeyNotFoundException("Client was not found.");
        if (tenantId is Guid tenant && tenant != managerId)
            throw new KeyNotFoundException("Client was not found.");
        return managerId;
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
