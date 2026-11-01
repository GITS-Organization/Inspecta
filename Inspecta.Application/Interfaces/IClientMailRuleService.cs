using Inspecta.Application.Contracts.Mail;

namespace Inspecta.Application.Interfaces;

public interface IClientMailRuleService
{
    Task<IReadOnlyList<ClientMailRuleResponse>> ListAsync(Guid? managerId, CancellationToken cancellationToken = default);
    Task<ClientMailRuleResponse> CreateAsync(Guid managerId, Guid actorId, CreateClientMailRuleRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientMailRuleResponse>> ListForClientAsync(Guid clientId, Guid? tenantId, CancellationToken cancellationToken = default);
    Task<ClientMailRuleResponse> AddForClientAsync(Guid clientId, Guid? tenantId, Guid actorId, AddClientEmailRequest request, CancellationToken cancellationToken = default);
    Task RemoveForClientAsync(Guid clientId, Guid ruleId, Guid? tenantId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid ruleId, CancellationToken cancellationToken = default);
}
