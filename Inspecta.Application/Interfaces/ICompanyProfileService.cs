using Inspecta.Application.Contracts.Company;

namespace Inspecta.Application.Interfaces;

public interface ICompanyProfileService
{
    /// <summary>The profile of a team, or <c>null</c> when it has none.</summary>
    Task<CompanyProfileResponse?> GetAsync(Guid managerId, CancellationToken cancellationToken = default);

    /// <summary>Creates or replaces the profile of a team (a technical manager).</summary>
    Task<CompanyProfileResponse> SaveAsync(Guid managerId, SaveCompanyProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>The logo of the team's company, or <c>null</c> when it has none.</summary>
    Task<byte[]?> GetLogoAsync(Guid managerId, CancellationToken cancellationToken = default);
}
