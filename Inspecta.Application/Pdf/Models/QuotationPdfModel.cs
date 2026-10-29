namespace Inspecta.Application.Pdf.Models;

/// <summary>Data of the technical and commercial offer PDF.</summary>
/// <param name="ProposalNumber">Reference in the page header and the cover letter, for example <c>INT/417/2026 rev01</c>.</param>
/// <param name="QuotationNumber">Number on the cover, for example <c>QT-2026-0142</c>.</param>
/// <param name="ContactName">Person the offer is addressed to ("Attention").</param>
/// <param name="Subject">Short title of what is quoted, for example <c>Mobile crane, Air receiver</c>.</param>
/// <param name="Company">Company details and fixed texts; the built-in company profile is used when empty.</param>
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
    QuotationCompanyProfile? Company = null);

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
