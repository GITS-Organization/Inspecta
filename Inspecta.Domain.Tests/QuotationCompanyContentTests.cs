using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Inspecta.Application.Pdf;
using Inspecta.Application.Pdf.Models;
using Inspecta.Infrastructure.Pdf;
using Inspecta.Infrastructure.Pdf.Templates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Inspecta.Domain.Tests;

/// <summary>
/// Step 1 of the company quotation content: the terms, bank accounts and contacts of the built-in company moved out of the layout into
/// <see cref="BuiltInQuotationContent"/>, and the PDF stayed the same.
/// </summary>
public sealed class QuotationCompanyContentTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// What Integrity's proposal printed before the content moved: the terms and the three bank accounts of the old default profile, and the
    /// contacts that were fixed in the proposal layout. A frozen copy, typed out on purpose: if someone edits the built-in content, a customer's
    /// printed quotations change and this fails.
    /// </summary>
    private static readonly QuotationCompanyContent Before = new(
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
        [
            new QuotationBankAccount("Integrity International for Inspection", "Commercial International Bank (CIB)", "EGP", "100072853193", "EG640010008900000100072853193", "CIBEEGCX089"),
            new QuotationBankAccount("Integrity International for Inspection", "Commercial International Bank (CIB)", "USD", "100072840806", "EG570010008900000100072840806", "CIBEEGCX089"),
            new QuotationBankAccount("Integrity International for Inspection", "Commercial International Bank (CIB)", "EUR", "100076863387", "EG820010003400000100076863387", "CIBEEGCX089")
        ],
        new QuotationContacts(
            ["21 Nakib Sherif Ramzy St, Smouha, Alexandria, Egypt", "522 Hassan Shafiq El-Masry, Al-Narges District, Cairo, Egypt"],
            ["+20 100 091 4066", "+20 109 998 3254"],
            ["rewan.khattab@integrity-intl.com", "nada.ali@integrity-intl.com"],
            "www.integrity-intl.com"));

    [Fact]
    public void TheBuiltInContentIsExactlyWhatTheProposalPrintedBefore() =>
        Assert.Equal(JsonSerializer.Serialize(Before, Json), JsonSerializer.Serialize(BuiltInQuotationContent.Instance, Json));

    [Fact]
    public void TheDefaultCompanyProfileNoLongerCarriesTheTermsOrTheBankAccounts()
    {
        Assert.Empty(DefaultCompanyProfile.Instance.Terms);
        Assert.Empty(DefaultCompanyProfile.Instance.BankAccounts);
    }

    private static CompanyBranding Branding(bool builtInDetails) => new(
        "Integrity", "Integrity", "Integrity", "", "", [], [], "", "", null, null, "#112233", "#445566",
        UsesBuiltInDetails: builtInDetails, KeepsBuiltInIdentity: builtInDetails);

    [Fact]
    public void IntegrityWithoutSavedContentStillGetsTwelvePagesAndTheSameContent()
    {
        using var provider = BuildProvider();
        var generator = provider.GetRequiredService<IPdfGenerator>();

        foreach (var branding in new CompanyBranding?[] { null, Branding(builtInDetails: true) })
        {
            var model = Model() with { Branding = branding, Company = branding?.ToQuotationProfile() };
            var company = QuotationEffectiveCompany.Of(model, DefaultCompanyProfile.Instance);

            Assert.Equal(JsonSerializer.Serialize(Before.Terms, Json), JsonSerializer.Serialize(company.Terms, Json));
            Assert.Equal(JsonSerializer.Serialize(Before.BankAccounts, Json), JsonSerializer.Serialize(company.BankAccounts, Json));
            Assert.Equal(Before.Contacts.Addresses, company.Addresses);
            Assert.Equal(Before.Contacts.Phones, company.Phones);
            Assert.Equal(Before.Contacts.Emails, company.Emails);
            Assert.Equal(Before.Contacts.Website, company.Website);
            Assert.Equal(12, PageCount(generator.Generate(model).Content));
        }
    }

    [Fact]
    public void ACompanyThatSavedNothingKeepsTodaysQuotation()
    {
        using var provider = BuildProvider();
        var own = new CompanyBranding(
            "Nour Inspection", "Nour Inspection", "Nour Inspection", "", "12 Nile St", ["+20 1"], ["info@nour.example", "second@nour.example"], "nour.example", "", null, null, "#112233", "#445566");
        var model = Model() with { Branding = own, Company = own.ToQuotationProfile() };

        var company = QuotationEffectiveCompany.Of(model, DefaultCompanyProfile.Instance);

        Assert.Empty(company.Terms);
        Assert.Empty(company.BankAccounts);
        Assert.Equal("12 Nile St", company.Address);
        Assert.Equal(["+20 1"], company.Phones);
        Assert.Equal("info@nour.example", company.Email);
        Assert.Equal("nour.example", company.Website);
        Assert.Equal(5, PageCount(provider.GetRequiredService<IPdfGenerator>().Generate(model).Content));
    }

    [Fact]
    public void ContentSavedForTheEarlierLayoutReplacesTermsAndBankAccountsAndOnlyTheContactsItFilled()
    {
        var own = new CompanyBranding(
            "Nour Inspection", "Nour Inspection", "Nour Inspection", "", "12 Nile St", ["+20 1"], ["info@nour.example"], "nour.example", "", null, null, "#112233", "#445566");
        var content = new QuotationCompanyContent(
            [new QuotationTermsSection("Payment", ["Within {days} days."])],
            [new QuotationBankAccount("Nour", "NBE", "EGP", "123", "", "")],
            new QuotationContacts(["Addr 1", "Addr 2"], [], ["a@nour.example", "b@nour.example"], string.Empty));
        var model = Model() with { Branding = own, Company = own.ToQuotationProfile(), Content = content };

        var company = QuotationEffectiveCompany.Of(model, DefaultCompanyProfile.Instance);

        Assert.Equal("Payment", Assert.Single(company.Terms).Title);
        Assert.Single(company.BankAccounts);
        Assert.Equal("Addr 1 / Addr 2", company.Address);
        Assert.Equal(["+20 1"], company.Phones);
        Assert.Equal("a@nour.example | b@nour.example", company.Email);
        Assert.Equal("nour.example", company.Website);
    }

    [Fact]
    public void ASnapshotSurvivesBeingStoredAsText()
    {
        var snapshot = new QuotationContentSnapshot(BuiltInQuotationContent.Instance, "Ahmed Rafat", "Senior Coordinator", "+20 100", "ahmed@example.com");

        var read = QuotationContentSnapshot.FromJson(snapshot.ToJson());

        Assert.NotNull(read);
        Assert.Equal(snapshot.ToJson(), read!.ToJson());
        Assert.Equal("Senior Coordinator", read.PreparedByTitle);
        Assert.Null(QuotationContentSnapshot.FromJson(null));
        Assert.Null(QuotationContentSnapshot.FromJson("not json"));
    }

    private static int PageCount(byte[] pdf)
    {
        var matches = Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Count (\d+)");
        return matches.Count == 0 ? -1 : matches.Max(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPdfGeneration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());
        return services.BuildServiceProvider();
    }

    internal static QuotationPdfModel Model() => new(
        ProposalNumber: "INT/417/2026 rev01",
        QuotationNumber: "QT-2026-0142",
        ClientName: "Delta Steel Co.",
        ContactName: "Eng. Reem",
        Subject: "Mobile crane, Air receiver",
        IssuedAt: new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero),
        ValidUntil: new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero),
        ValidForDays: 14,
        PreparedBy: "Ahmed Rafat",
        PreparedByTitle: "Coordinator",
        Message: "Please find our offer below.",
        Items:
        [
            new QuotationPdfItem("Mobile crane", "Mobile crane - Liebherr LTM 1100, 100 t", "13351021", 1, 1_200_000, "Per Visit"),
            new QuotationPdfItem("Air receiver", "Air receiver - 3000 L", null, 2, null, "Per Visit")
        ],
        ScopeOfWork: ["Load and functional test", "Visual inspection"],
        Notes: ["Pricing excludes VAT."]);
}
