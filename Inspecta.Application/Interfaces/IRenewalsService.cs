using Inspecta.Application.Contracts.Renewals;

namespace Inspecta.Application.Interfaces;

public interface IRenewalsService
{
    /// <param name="window"><c>overdue</c>, <c>30</c>, <c>60</c> or <c>90</c> (default 30).</param>
    Task<RenewalsListResponse> ListAsync(Guid managerId, string? window, string? search, int page, int pageSize, CancellationToken cancellationToken = default);
}
