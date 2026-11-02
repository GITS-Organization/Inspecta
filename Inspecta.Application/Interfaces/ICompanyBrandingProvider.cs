using Inspecta.Application.Pdf.Models;

namespace Inspecta.Application.Interfaces;

/// <summary>The identity of the company a team belongs to, for its certificates, quotations and emails.</summary>
public interface ICompanyBrandingProvider
{
    /// <summary>The company profile of the team, or <c>null</c> when it has none (the built-in profile is then used).</summary>
    Task<CompanyBranding?> GetAsync(Guid? managerId, CancellationToken cancellationToken = default);
}
