namespace Inspecta.Application.Pdf.Models;

/// <summary>
/// The identity of a company as the PDFs and emails print it. A team without a company profile has none (<c>null</c>) and gets the
/// built-in profile of the first customer.
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
    bool UsesBuiltInDetails = false)
{
    /// <summary>
    /// The company details of a quotation, or <c>null</c> when the profile keeps the built-in company details. Its long texts (about us, services, terms, bank accounts) are not part of the profile yet, so
    /// they are empty and the pages that would carry them are left out of the quotation.
    /// </summary>
    public QuotationCompanyProfile? ToQuotationProfile() =>
        UsesBuiltInDetails ? null : new(Name, Address, Phones, Emails.FirstOrDefault() ?? string.Empty, Website, AboutUs: string.Empty, Vision: string.Empty,
            Services: [], Terms: [], BankAccounts: [], ServicesLabel: "Our Services");
}
