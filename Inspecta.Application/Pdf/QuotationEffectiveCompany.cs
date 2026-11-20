using Inspecta.Application.Pdf.Models;

namespace Inspecta.Application.Pdf;

/// <summary>
/// Puts the content of a quotation (terms, bank accounts, contacts) over the company details, so the templates read one company and do not
/// care where each value came from.
/// </summary>
public static class QuotationEffectiveCompany
{
    /// <summary>The built-in company (with or without a saved profile that keeps the built-in details) is drawn in its own proposal design.</summary>
    public static bool UsesBuiltInLayout(CompanyBranding? branding) => branding is null or { UsesBuiltInDetails: true };

    /// <summary>
    /// The company details the quotation is drawn with. <paramref name="builtInProfile"/> stands in for a model without a company of its own.
    /// With no <see cref="QuotationPdfModel.Content"/> the built-in layout prints the built-in content and another company prints what it
    /// always did: its profile details and no terms page.
    /// </summary>
    public static QuotationCompanyProfile Of(QuotationPdfModel model, QuotationCompanyProfile builtInProfile)
    {
        var builtIn = UsesBuiltInLayout(model.Branding);
        var profile = model.Company ?? builtInProfile;
        var content = model.Content ?? (builtIn ? BuiltInQuotationContent.Instance : null);
        if (content is null) return profile;

        var contacts = content.Contacts;
        if (builtIn)
        {
            // The proposal design prints the lists as they are, so a company that cleared a list prints none of it.
            return profile with
            {
                Terms = content.Terms,
                BankAccounts = content.BankAccounts,
                Address = string.Join(" / ", contacts.Addresses),
                Addresses = contacts.Addresses,
                Phones = contacts.Phones,
                Email = contacts.Emails.FirstOrDefault() ?? string.Empty,
                Emails = contacts.Emails,
                Website = contacts.Website
            };
        }

        // The earlier layout keeps its profile details for whatever contact the company left empty.
        return profile with
        {
            Terms = content.Terms,
            BankAccounts = content.BankAccounts,
            Address = contacts.Addresses.Count > 0 ? string.Join(" / ", contacts.Addresses) : profile.Address,
            Phones = contacts.Phones.Count > 0 ? contacts.Phones : profile.Phones,
            Email = contacts.Emails.Count > 0 ? string.Join(" | ", contacts.Emails) : profile.Email,
            Website = string.IsNullOrWhiteSpace(contacts.Website) ? profile.Website : contacts.Website
        };
    }
}
