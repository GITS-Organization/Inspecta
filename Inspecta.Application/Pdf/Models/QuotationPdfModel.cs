namespace Inspecta.Application.Pdf.Models;

/// <summary>Data of the technical and commercial offer PDF. The layout is still a rough placeholder.</summary>
public sealed record QuotationPdfModel(
    string ProposalNumber,
    string ClientName,
    DateTimeOffset IssuedAt,
    DateTimeOffset ValidUntil,
    string? Message,
    IReadOnlyList<QuotationPdfItem> Items,
    IReadOnlyList<string> ScopeOfWork,
    IReadOnlyList<string> Notes);

/// <summary>One line of the scope of work and budget tables.</summary>
public sealed record QuotationPdfItem(
    string Description,
    string? PrNumber,
    int Quantity,
    long? UnitPricePiastres,
    string PricingUnit)
{
    public long? TotalPiastres => UnitPricePiastres is long price ? checked(price * Quantity) : null;
}
