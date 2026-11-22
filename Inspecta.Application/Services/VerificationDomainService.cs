using Inspecta.Application.Certificates;
using Inspecta.Application.Contracts.Company;
using Inspecta.Application.Exceptions;
using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

/// <summary>A company's own certificate verification addresses: list them, choose the primary one, remove an old one.</summary>
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
        var check = VerificationHost.Check(host, VerificationHost.HostOf((options ?? new CertificateOptions()).PublicBaseUrl));
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

    /// <summary>
    /// Removes an address. The primary one cannot be removed (choose another primary first): certificates are issued with it. An old,
    /// non-primary address can go.
    /// </summary>
    public async Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var domain = (await domains.ListAsync(companyId, tracking: true, cancellationToken)).FirstOrDefault(value => value.Id == id)
            ?? throw new KeyNotFoundException("Verification address was not found.");
        if (domain.IsPrimary)
            throw new AuthException("primary_domain", "This is the address new certificates use. Choose another primary address first.", 409);
        domains.Remove(domain);
        await domains.SaveAsync(cancellationToken);
    }

    // The primary first, then by name: ordering in memory (a company has a handful, and SQLite cannot order by a date).
    private static IReadOnlyList<VerificationDomainResponse> Map(IEnumerable<CompanyVerificationDomain> rows) =>
        rows.OrderByDescending(domain => domain.IsPrimary).ThenBy(domain => domain.Host, StringComparer.Ordinal).Select(Map).ToArray();

    private static VerificationDomainResponse Map(CompanyVerificationDomain domain) =>
        new(domain.Id, domain.Host, domain.IsPrimary, domain.CreatedAt);
}
