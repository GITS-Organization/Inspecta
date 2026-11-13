namespace Inspecta.Application.Pdf.Models;

/// <summary>
/// Data of the invoice PDF: the issuer, the recipient, the price lines as the quotation had them and the totals, with what was paid and what is left.
/// An empty detail (a tax number the company never gave) is left out of the page instead of being printed blank.
/// </summary>
/// <param name="IssuerName">The company's own name, or <c>null</c> to print the built-in company.</param>
/// <param name="IssuerAddress">The company's own address, or <c>null</c> to print the built-in company.</param>
/// <param name="Branding">Logo of the company; <c>null</c> means the built-in one.</param>
public sealed record InvoicePdfModel(
    string Number,
    DateOnly IssuedOn,
    string? IssuerName,
    string? IssuerTaxRegistrationNumber,
    string? IssuerActivityCode,
    string? IssuerAddress,
    string? QuotationReference,
    string ClientName,
    string? ClientTaxNumber,
    string? ClientAddress,
    IReadOnlyList<InvoicePdfLine> Lines,
    long SubtotalPiastres,
    decimal DiscountPercent,
    long DiscountPiastres,
    decimal VatPercent,
    long VatPiastres,
    long TotalPiastres,
    long PaidPiastres,
    long BalancePiastres,
    CompanyBranding? Branding = null);

/// <summary>One row of the lines table. <c>CodeName</c> is the type of the line (for example "Mobile crane"); there is no item code.</summary>
public sealed record InvoicePdfLine(string CodeName, string Description, int Quantity, string UnitType, long? UnitPricePiastres, long TotalPiastres);
