using Inspecta.Application.Contracts.Mail;

namespace Inspecta.Application.Interfaces;

public interface IClientMailRuleService
{
    Task<IReadOnlyList<ClientMailRuleResponse>> ListAsync(Guid? managerId, CancellationToken cancellationToken = default);
    Task<ClientMailRuleResponse> CreateAsync(Guid managerId, Guid actorId, CreateClientMailRuleRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid ruleId, CancellationToken cancellationToken = default);
}
