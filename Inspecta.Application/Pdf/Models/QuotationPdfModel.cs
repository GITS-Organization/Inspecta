namespace Inspecta.Application.Pdf.Models;

/// <summary>Data of the technical and commercial offer PDF.</summary>
/// <param name="ProposalNumber">Reference in the page header and the cover letter, for example <c>INT/417/2026 rev01</c>.</param>
/// <param name="QuotationNumber">Number on the cover, for example <c>QT-2026-0142</c>.</param>
/// <param name="ContactName">Person the offer is addressed to ("Attention").</param>
/// <param name="Subject">Short title of what is quoted, for example <c>Mobile crane, Air receiver</c>.</param>
/// <param name="Company">Company details and fixed texts; the built-in company profile is used when empty.</param>
/// <param name="Branding">The identity of the team's company (logo, colours); empty means the built-in one.</param>
/// <param name="DiscountPercent">Percentage taken off the total of the lines; above 0 the budget table shows the discount and a new total under the total.</param>
/// <param name="Content">The company's terms, bank accounts and contacts for this quotation. Left out: the built-in content for the built-in layout, nothing for another company.</param>
/// <param name="PreparedByPhone">Printed under the signature when there is one.</param>
/// <param name="PreparedByEmail">Printed under the signature, after the phone, when there is one.</param>
/// <param name="VatPercent">VAT on the total after the discount; above 0 the budget table shows the VAT and the total including it, 0 prints nothing about VAT.</param>
public sealed record QuotationPdfModel(
    string ProposalNumber,
    string QuotationNumber,
    string ClientName,
    string? ContactName,
    string Subject,
    DateTimeOffset IssuedAt,
    DateTimeOffset ValidUntil,
    int ValidForDays,
    string PreparedBy,
    string PreparedByTitle,
    string? Message,
    IReadOnlyList<QuotationPdfItem> Items,
    IReadOnlyList<string> ScopeOfWork,
    IReadOnlyList<string> Notes,
    QuotationCompanyProfile? Company = null,
    CompanyBranding? Branding = null,
    decimal DiscountPercent = 0,
    decimal VatPercent = 0,
    QuotationCompanyContent? Content = null,
    string? PreparedByPhone = null,
    string? PreparedByEmail = null);

/// <summary>One line of the scope of work and budget tables.</summary>
/// <param name="Type">Short name used in the numbered scope list, for example <c>Mobile crane</c>.</param>
public sealed record QuotationPdfItem(
    string Type,
    string Description,
    string? PrNumber,
    int Quantity,
    long? UnitPricePiastres,
    string PricingUnit)
{
    public long? TotalPiastres => UnitPricePiastres is long price ? checked(price * Quantity) : null;
}
