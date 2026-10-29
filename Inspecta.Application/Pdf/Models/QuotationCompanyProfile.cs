namespace Inspecta.Application.Pdf.Models;

/// <summary>
/// Company details and the fixed texts of the quotation (about us, vision, services, terms, bank accounts).
/// Text may contain <c>**bold**</c> markers.
/// </summary>
public sealed record QuotationCompanyProfile(
    string Name,
    string Address,
    IReadOnlyList<string> Phones,
    string Email,
    string Website,
    string AboutUs,
    string Vision,
    IReadOnlyList<QuotationServiceSection> Services,
    IReadOnlyList<QuotationTermsSection> Terms,
    IReadOnlyList<QuotationBankAccount> BankAccounts);

/// <summary>A titled table of services, for example "4-1 Industrial Services".</summary>
public sealed record QuotationServiceSection(
    string Title,
    string ServiceColumn,
    string ScopeColumn,
    bool Numbered,
    IReadOnlyList<QuotationServiceGroup> Groups);

/// <summary>Services under an optional heading row.</summary>
public sealed record QuotationServiceGroup(string? Heading, IReadOnlyList<QuotationServiceLine> Lines);

public sealed record QuotationServiceLine(string Service, string Scope);

/// <summary>A titled list of terms. <c>{days}</c> in a point is replaced with the validity in days.</summary>
public sealed record QuotationTermsSection(string Title, IReadOnlyList<string> Points, bool ShowBankAccounts = false);

public sealed record QuotationBankAccount(
    string Beneficiary,
    string Bank,
    string Currency,
    string AccountNumber,
    string Iban,
    string Swift);
