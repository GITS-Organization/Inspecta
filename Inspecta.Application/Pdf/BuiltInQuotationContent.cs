using Inspecta.Application.Pdf.Models;

namespace Inspecta.Application.Pdf;

/// <summary>
/// The terms and conditions, the bank accounts and the contacts the built-in company (Integrity International for Inspection) prints on its
/// proposals. A company drawn in the built-in layout that has saved nothing about its quotation gets these, so its quotations do not change;
/// the first time its manager saves, the editor starts from these same values. They are code, not data: nothing about them is written to the
/// database.
/// </summary>
public static class BuiltInQuotationContent
{
    public static readonly QuotationCompanyContent Instance = new(
        Terms:
        [
            new QuotationTermsSection("Invoicing and Payment",
            [
                "INTEGRITY will issue and submit an invoice upon completion of the job.",
                "The Client agrees to pay **50 %** as advance.",
                "The Client agrees to pay **50 %** against the reports."
            ]),
            new QuotationTermsSection("Offer Validity",
            [
                "This proposal is made in good faith and remains valid for **{days} days** from the date of issue."
            ]),
            new QuotationTermsSection("Payment Details",
            [
                "Payments can be made via cash or check.",
                "Bank details will be provided upon request."
            ]),
            new QuotationTermsSection("Bank Account", [], ShowBankAccounts: true),
            new QuotationTermsSection("INTEGRITY Obligations",
            [
                "Execute the scope of work to a professional technical standard, in line with the referenced codes and standards.",
                "Comply with site HSE regulations and equip our engineers with full PPE.",
                "Maintain strict confidentiality of all client information, data, and findings obtained during the work, and not disclose them to any third party without prior written consent (Non-Disclosure Agreement, NDA)."
            ]),
            new QuotationTermsSection("Client Obligations",
            [
                "Provide the necessary site permits and safe access for Integrity International Inspection engineers.",
                "Provide all drawings and technical information required to execute the scope (equipment lists, layouts, etc.).",
                "Ensure the security of the assigned crew and their equipment while on site.",
                "Provide machine identification data required for analysis, including bearing designations, cross-section details, and gear tooth counts, as applicable."
            ])
        ],
        BankAccounts:
        [
            new QuotationBankAccount(
                "Integrity International for Inspection", "Commercial International Bank (CIB)", "EGP",
                "100072853193", "EG640010008900000100072853193", "CIBEEGCX089"),
            new QuotationBankAccount(
                "Integrity International for Inspection", "Commercial International Bank (CIB)", "USD",
                "100072840806", "EG570010008900000100072840806", "CIBEEGCX089"),
            new QuotationBankAccount(
                "Integrity International for Inspection", "Commercial International Bank (CIB)", "EUR",
                "100076863387", "EG820010003400000100076863387", "CIBEEGCX089")
        ],
        Contacts: new QuotationContacts(
            Addresses:
            [
                "21 Nakib Sherif Ramzy St, Smouha, Alexandria, Egypt",
                "522 Hassan Shafiq El-Masry, Al-Narges District, Cairo, Egypt"
            ],
            Phones: ["+20 100 091 4066", "+20 109 998 3254"],
            Emails: ["rewan.khattab@integrity-intl.com", "nada.ali@integrity-intl.com"],
            Website: "www.integrity-intl.com"));
}
