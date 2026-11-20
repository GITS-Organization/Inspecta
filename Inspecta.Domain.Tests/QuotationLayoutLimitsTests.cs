using System.Text;
using System.Text.RegularExpressions;
using Inspecta.Application.Pdf;
using Inspecta.Application.Pdf.Models;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Pdf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inspecta.Domain.Tests;

/// <summary>
/// A company can fill every limit of its quotation content, with words that never break. Both layouts must still draw it, and the built-in
/// proposal must keep its twelve pages when only the contacts are at their limits.
/// </summary>
public sealed class QuotationLayoutLimitsTests
{
    private static string Word(char letter, int length) => new(letter, length);

    private static QuotationContacts FullContacts() => new QuotationContacts(
        [Word('a', CompanyQuotationContent.MaxAddress), Word('b', CompanyQuotationContent.MaxAddress), Word('c', CompanyQuotationContent.MaxAddress)],
        [Word('1', CompanyQuotationContent.MaxPhone), Word('2', CompanyQuotationContent.MaxPhone), Word('3', CompanyQuotationContent.MaxPhone)],
        new[] { 0, 1, 2 }.Select(index => Word((char)('d' + index), CompanyQuotationContent.MaxEmail - 5) + "@x.co").ToArray(),
        Word('w', CompanyQuotationContent.MaxWebsite));

    private static QuotationCompanyContent FullContent() => new(
        Enumerable.Range(0, CompanyQuotationContent.MaxTermSections)
            .Select(index => new QuotationTermsSection(
                Word('T', CompanyQuotationContent.MaxTermTitle),
                Enumerable.Range(0, CompanyQuotationContent.MaxPointsPerSection).Select(_ => Word('p', CompanyQuotationContent.MaxPoint)).ToArray(),
                ShowBankAccounts: index == 1))
            .ToArray(),
        Enumerable.Range(0, CompanyQuotationContent.MaxBankAccounts)
            .Select(_ => new QuotationBankAccount(
                Word('B', CompanyQuotationContent.MaxBankName), Word('K', CompanyQuotationContent.MaxBankName), Word('E', CompanyQuotationContent.MaxCurrency),
                Word('9', CompanyQuotationContent.MaxAccountNumber), Word('I', CompanyQuotationContent.MaxIban), "CIBEEGCX089"))
            .ToArray(),
        FullContacts());

    private static CompanyBranding Nour() => new(
        "Nour Inspection", "Nour Inspection", "Nour Inspection", "", "12 Nile St", ["+20 1"], ["info@nour.example"], "nour.example", "", null, null, "#112233", "#445566");

    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPdfGeneration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());
        return services.BuildServiceProvider();
    }

    private static int PageCount(byte[] pdf)
    {
        var matches = Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Count (\d+)");
        return matches.Count == 0 ? -1 : matches.Max(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TheBuiltInProposalKeepsTwelvePagesWithEveryContactAtItsLimit()
    {
        using var provider = Provider();
        var content = BuiltInQuotationContent.Instance with { Contacts = FullContacts() };

        var pdf = provider.GetRequiredService<IPdfGenerator>().Generate(QuotationCompanyContentTests.Model() with { Content = content });

        Assert.Equal(12, PageCount(pdf.Content));
    }

    [Fact]
    public void TheBuiltInProposalDrawsEveryLimitFilledWithWordsThatNeverBreak()
    {
        using var provider = Provider();

        var pdf = provider.GetRequiredService<IPdfGenerator>().Generate(
            QuotationCompanyContentTests.Model() with { Content = FullContent(), PreparedByTitle = Word('J', 100), PreparedByPhone = Word('1', 40), PreparedByEmail = Word('m', 300) + "@x.co" });

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf.Content, 0, 5));
        Assert.True(PageCount(pdf.Content) > 12);
    }

    [Fact]
    public void TheEarlierLayoutDrawsEveryLimitFilledWithWordsThatNeverBreak()
    {
        using var provider = Provider();
        var own = Nour();

        var pdf = provider.GetRequiredService<IPdfGenerator>().Generate(
            QuotationCompanyContentTests.Model() with
            {
                Branding = own,
                Company = own.ToQuotationProfile(),
                Content = FullContent(),
                PreparedByTitle = Word('J', 100),
                PreparedByPhone = Word('1', 40),
                PreparedByEmail = Word('m', 300) + "@x.co"
            });

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf.Content, 0, 5));
        Assert.True(PageCount(pdf.Content) > 5);
    }
}
