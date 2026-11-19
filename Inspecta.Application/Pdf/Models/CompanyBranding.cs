namespace Inspecta.Application.Pdf.Models;

/// <summary>
/// The identity of a company as the PDFs and emails print it. <c>null</c> means the built-in identity of the first customer, which only a
/// company that keeps it (one that existed before new companies stopped getting it) is given; a company created since always has a branding,
/// under its own name at least.
/// </summary>
public sealed record CompanyBranding(
    string Name,
    string CertificateTitle,
    string IssuerName,
    string Tagline,
    string Address,
    IReadOnlyList<string> Phones,
    IReadOnlyList<string> Emails,
    string Website,
    string SignerName,
    byte[]? LogoPng,
    byte[]? AccreditationsPng,
    string PrimaryColor,
    string AccentColor,
    bool UsesBuiltInDetails = false,
    string? TaxRegistrationNumber = null,
    string? ActivityCode = null,
    bool KeepsBuiltInIdentity = false)
{
    /// <summary>
    /// Whether what this company leaves out may be filled from the first customer's built-in identity (logo, accreditation marks, the issuer
    /// on the QR page, the wording of the standing notes): only for the built-in details, or for a company that keeps the built-in identity.
    /// </summary>
    public bool MayUseBuiltInIdentity => UsesBuiltInDetails || KeepsBuiltInIdentity;

    /// <summary>
    /// The company details of a quotation, or <c>null</c> when the profile keeps the built-in company details. Its long texts (about us, services, terms, bank accounts) are not part of the profile yet, so
    /// they are empty and the pages that would carry them are left out of the quotation.
    /// </summary>
    public QuotationCompanyProfile? ToQuotationProfile() =>
        UsesBuiltInDetails ? null : new(Name, Address, Phones, Emails.FirstOrDefault() ?? string.Empty, Website, AboutUs: string.Empty, Vision: string.Empty,
            Services: [], Terms: [], BankAccounts: [], ServicesLabel: "Our Services");
}
