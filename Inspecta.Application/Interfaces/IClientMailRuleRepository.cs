using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IClientMailRuleRepository
{
    Task<IReadOnlyList<ClientMailRule>> ListAsync(Guid? managerId, CancellationToken cancellationToken = default);
    Task<ClientMailRule?> GetByIdAsync(Guid ruleId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid managerId, string pattern, CancellationToken cancellationToken = default);
    Task<Guid?> GetClientManagerIdAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task AddAsync(ClientMailRule rule, CancellationToken cancellationToken = default);
    Task DeleteAsync(ClientMailRule rule, CancellationToken cancellationToken = default);
}
