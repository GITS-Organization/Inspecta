namespace Inspecta.Application.Interfaces;

/// <summary>
/// The one question every flow asks about a company before it lets someone work in it: does it exist, is it switched on, and is its
/// subscription valid. Replaces the copies of "load the manager and check it" that used to sit in each service.
/// </summary>
public interface ICompanyGate
{
    /// <summary>The company exists and is switched on (a lapsed subscription does not matter here).</summary>
    Task<bool> IsActiveAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The company exists, is switched on and its subscription is active and not past its end date.</summary>
    Task<bool> IsUsableAsync(Guid companyId, CancellationToken cancellationToken = default);
}
