using Inspecta.Application.Contracts.Company;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf.Models;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Services;

public sealed class CompanyProfileService(
    ICompanyProfileRepository profileRepository,
    IUserRepository userRepository,
    ICompanyRepository? companies = null) : ICompanyProfileService, ICompanyBrandingProvider
{
    public async Task<CompanyProfileResponse?> GetAsync(Guid managerId, CancellationToken cancellationToken = default) =>
        (await profileRepository.GetByManagerAsync(managerId, tracking: false, cancellationToken)) is { } profile ? Map(profile) : null;

    public async Task<CompanyProfileResponse> SaveAsync(
        Guid managerId,
        SaveCompanyProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (companies is not null && await companies.GetByIdAsync(managerId, tracking: false, cancellationToken) is null)
            throw new KeyNotFoundException("Company was not found.");

        // Checked before anything is written: a bad image or colour saves nothing.
        var logo = Decode(request.LogoBase64, "logo");
        var accreditations = Decode(request.AccreditationsBase64, "accreditations image");

        var profile = await profileRepository.GetByManagerAsync(managerId, tracking: true, cancellationToken);
        var isNew = profile is null;
        profile ??= CompanyProfile.Create(managerId);
        profile.Update(
            request.Name, request.CertificateTitle, request.IssuerName, request.Tagline, request.Address, request.Phones,
            request.Emails, request.Website, request.SignerName, request.PrimaryColor, request.AccentColor);
        profile.SetTaxDetails(request.TaxRegistrationNumber, request.ActivityCode);
        if (logo.Changed) profile.SetLogo(logo.Content);
        if (accreditations.Changed) profile.SetAccreditations(accreditations.Content);

        if (isNew) await profileRepository.AddAsync(profile, cancellationToken);
        else await profileRepository.SaveAsync(cancellationToken);
        return Map(profile);
    }

    public void Validate(SaveCompanyProfileRequest request)
    {
        var profile = CompanyProfile.Create(Guid.NewGuid());
        profile.Update(
            request.Name, request.CertificateTitle, request.IssuerName, request.Tagline, request.Address, request.Phones,
            request.Emails, request.Website, request.SignerName, request.PrimaryColor, request.AccentColor);
        profile.SetTaxDetails(request.TaxRegistrationNumber, request.ActivityCode);
        profile.SetLogo(Decode(request.LogoBase64, "logo").Content);
        profile.SetAccreditations(Decode(request.AccreditationsBase64, "accreditations image").Content);
    }

    public async Task<byte[]?> GetLogoAsync(Guid managerId, CancellationToken cancellationToken = default) =>
        (await profileRepository.GetByManagerAsync(managerId, tracking: false, cancellationToken))?.LogoPng;

    public async Task<CompanyBranding?> GetAsync(Guid? managerId, CancellationToken cancellationToken = default)
    {
        if (managerId is not Guid id) return null;
        return await profileRepository.GetByManagerAsync(id, tracking: false, cancellationToken) is { } profile ? ToBranding(profile) : null;
    }

    private static CompanyBranding ToBranding(CompanyProfile profile) => new(
        profile.Name,
        profile.CertificateTitle ?? profile.Name,
        profile.IssuerName ?? profile.Name,
        profile.Tagline ?? string.Empty,
        profile.Address,
        profile.Phones,
        profile.Emails,
        profile.Website,
        profile.SignerName ?? string.Empty,
        profile.LogoPng,
        profile.AccreditationsPng,
        profile.PrimaryColor,
        profile.AccentColor,
        profile.UsesBuiltInDetails,
        profile.TaxRegistrationNumber,
        profile.ActivityCode);

    private static CompanyProfileResponse Map(CompanyProfile profile) => new(
        profile.ManagerId,
        profile.Name,
        profile.CertificateTitle,
        profile.IssuerName,
        profile.Tagline,
        profile.Address,
        profile.Phones,
        profile.Emails,
        profile.Website,
        profile.SignerName,
        profile.PrimaryColor,
        profile.AccentColor,
        profile.LogoPng is { Length: > 0 },
        profile.AccreditationsPng is { Length: > 0 },
        profile.LogoPng is { Length: > 0 } ? $"/api/v1/branding/{profile.ManagerId}/logo.png" : null,
        profile.UpdatedAt,
        profile.UsesBuiltInDetails,
        profile.TaxRegistrationNumber,
        profile.ActivityCode);

    // Left out: unchanged. Empty: removed. Otherwise the image.
    private static (bool Changed, byte[]? Content) Decode(string? base64, string what)
    {
        if (base64 is null) return (false, null);
        if (string.IsNullOrWhiteSpace(base64)) return (true, null);
        try { return (true, Convert.FromBase64String(base64)); }
        catch (FormatException) { throw new ArgumentException($"The {what} is not valid base64."); }
    }
}
