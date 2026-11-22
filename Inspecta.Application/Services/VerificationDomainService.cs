using Inspecta.Application.Certificates;
using Inspecta.Application.Contracts.Company;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

/// <summary>A company's own certificate verification addresses: list them and choose the primary one. A host, once registered, stays reserved for its company (there is no delete), so another company can never take it over and break the QR codes already printed.</summary>
public sealed class VerificationDomainService(ICompanyVerificationDomainRepository domains, CertificateOptions? options = null)
{
    public async Task<IReadOnlyList<VerificationDomainResponse>> ListAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        Map(await domains.ListAsync(companyId, tracking: false, cancellationToken));

    /// <summary>
    /// Makes <paramref name="host"/> the company's primary address. A new host is added; a host the company already has is promoted; the
    /// old primary stays (so QR codes already printed keep working). A host another company owns is refused (409).
    /// </summary>
    public async Task<VerificationDomainResponse> SetPrimaryAsync(Guid companyId, string? host, CancellationToken cancellationToken = default)
    {
        var configured = options ?? new CertificateOptions();
        var check = VerificationHost.Check(host, VerificationHost.HostOf(configured.PublicBaseUrl), configured.ReservedHosts);
        if (!check.IsValid) throw new AuthException(check.ErrorCode!, check.Message!, 400);

        var owner = await domains.FindCompanyIdByHostAsync(check.Host!, cancellationToken);
        if (owner is { } other && other != companyId)
            throw new AuthException("host_taken", "This address is already used by another company.", 409);

        var own = await domains.ListAsync(companyId, tracking: true, cancellationToken);
        var existing = own.FirstOrDefault(domain => domain.Host == check.Host);
        if (existing is { IsPrimary: true } && own.Count(domain => domain.IsPrimary) == 1) return Map(existing);

        foreach (var domain in own.Where(domain => domain.IsPrimary)) domain.Demote();
        if (existing is null)
        {
            existing = CompanyVerificationDomain.Create(companyId, check.Host!, isPrimary: true);
            await domains.AddAsync(existing, cancellationToken);
        }
        else existing.MakePrimary();
        await domains.SaveAsync(cancellationToken);
        return Map(existing);
    }

    // The primary first, then by name: ordering in memory (a company has a handful, and SQLite cannot order by a date).
    private static IReadOnlyList<VerificationDomainResponse> Map(IEnumerable<CompanyVerificationDomain> rows) =>
        rows.OrderByDescending(domain => domain.IsPrimary).ThenBy(domain => domain.Host, StringComparer.Ordinal).Select(Map).ToArray();

    private static VerificationDomainResponse Map(CompanyVerificationDomain domain) =>
        new(domain.Id, domain.Host, domain.IsPrimary, domain.CreatedAt);
}
