using Inspecta.Application.Executive;

namespace Inspecta.Application.Interfaces;

public interface IExecutiveDataSource
{
    /// <summary>
    /// The rows of the caller company (the data layer limits them by the token). The platform owner gets every company, or one when
    /// <paramref name="managerId"/> is given.
    /// </summary>
    Task<ExecutiveData> LoadAsync(Guid? managerId, CancellationToken cancellationToken = default);
}
