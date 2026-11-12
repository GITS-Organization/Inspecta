using Inspecta.Application.Contracts.Companies;

namespace Inspecta.Application.Interfaces;

public interface ICompanyService
{
    Task<IReadOnlyList<CompanyResponse>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>The company, or null when it does not exist.</summary>
    Task<CompanyResponse?> GetAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>Creates the company and its first account, the company admin, in one go (the platform owner hands the company over with it).</summary>
    Task<CreatedCompanyResponse> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default);

    Task<CompanyResponse> RenameAsync(Guid companyId, string name, CancellationToken cancellationToken = default);

    /// <summary>Switches the whole company on or off: nobody in it can sign in while it is off.</summary>
    Task<CompanyResponse> SetStatusAsync(Guid companyId, bool isActive, CancellationToken cancellationToken = default);

    Task<CompanyResponse> UpdateSubscriptionAsync(Guid companyId, UpdateCompanySubscriptionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Chooses the technical manager every new request of the company starts with (an active technical manager of that company).</summary>
    Task<CompanyResponse> SetDefaultReviewerAsync(Guid companyId, Guid reviewerId, CancellationToken cancellationToken = default);
}
