using Inspecta.Application.Interfaces;

namespace Inspecta.Application.Services;

public sealed class CompanyGate(ICompanyRepository companies) : ICompanyGate
{
    public async Task<bool> IsActiveAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await companies.GetByIdAsync(companyId, tracking: false, cancellationToken) is { IsActive: true };

    public async Task<bool> IsUsableAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await companies.GetByIdAsync(companyId, tracking: false, cancellationToken) is { } company && company.IsUsable();
}
