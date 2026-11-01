using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

/// <summary>What a waiting request takes over from its client.</summary>
public sealed record ClientSummary(string Name, string? Location);

public interface IClientMailRuleRepository
{
    Task<IReadOnlyList<ClientMailRule>> ListAsync(Guid? managerId, CancellationToken cancellationToken = default);
    Task<ClientMailRule?> GetByIdAsync(Guid ruleId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid managerId, string pattern, CancellationToken cancellationToken = default);
    Task<Guid?> GetClientManagerIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<ClientSummary?> GetClientSummaryAsync(Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns the waiting "maybe" emails of the team that come from <paramref name="rule"/>'s sender into requests of the
    /// client. Returns how many were turned.
    /// </summary>
    Task<int> PromoteMaybesAsync(ClientMailRule rule, ClientSummary client, CancellationToken cancellationToken = default);

    Task AddAsync(ClientMailRule rule, CancellationToken cancellationToken = default);
    Task DeleteAsync(ClientMailRule rule, CancellationToken cancellationToken = default);
}
