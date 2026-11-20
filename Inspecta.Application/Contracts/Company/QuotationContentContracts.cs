using Inspecta.Domain.Entities;

namespace Inspecta.Application.Contracts.Company;

/// <summary>
/// What a company prints in its quotations. Everything is replaced by what is sent: a list left out is saved empty. Saving an empty
/// <c>terms</c> removes the terms page of the quotation.
/// </summary>
public sealed record SaveQuotationContentRequest
{
    /// <summary>At most 12 sections (title 1 to 120 characters; at most 15 points of 1 to 600 characters; at most one section with <c>showBankAccounts</c>). <c>{days}</c> in a point is replaced with the validity in days, <c>**text**</c> is bold.</summary>
    public IReadOnlyList<QuotationContentTerm>? Terms { get; init; }

    /// <summary>At most 5. Beneficiary and bank are required; give an account number or an IBAN; the SWIFT code, when given, has 8 or 11 letters or digits.</summary>
    public IReadOnlyList<QuotationContentBankAccount>? BankAccounts { get; init; }

    /// <summary>At most 3 addresses, 3 phones and 3 emails; printed on the cover and in the footer of every page.</summary>
    public QuotationContentContacts? Contacts { get; init; }
}

/// <summary>The person who saved the content last.</summary>
public sealed record QuotationContentAuthor(Guid Id, string FullName);

/// <summary>The content of a company's quotations.</summary>
/// <param name="IsDefault">True when the company never saved any: the lists are the default (the built-in content for the built-in company, empty for any other).</param>
public sealed record QuotationContentResponse(
    IReadOnlyList<QuotationContentTerm> Terms,
    IReadOnlyList<QuotationContentBankAccount> BankAccounts,
    QuotationContentContacts Contacts,
    bool IsDefault,
    DateTimeOffset? UpdatedAt,
    QuotationContentAuthor? UpdatedBy);
