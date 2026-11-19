using Inspecta.Application.Contracts.Renewals;

namespace Inspecta.Application.Interfaces;

public interface IRenewalsRepository
{
    /// <summary>
    /// The clients of the company that have an active, not archived machine overdue or due before <paramref name="dueBefore"/>, with those machines, their
    /// contacts that have an email, and whether each machine is on an open request. Retired and archived machines and archived clients are left out.
    /// </summary>
    Task<IReadOnlyList<RenewalClientRows>> ListDueAsync(Guid managerId, DateTimeOffset dueBefore, CancellationToken cancellationToken = default);
}
