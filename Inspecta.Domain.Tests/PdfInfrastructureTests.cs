using System.Text;
using Inspecta.Application.Pdf;
using Inspecta.Application.Pdf.Models;
using Inspecta.Infrastructure.Pdf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Inspecta.Domain.Tests;

public sealed class PdfInfrastructureTests
{
    [Fact]
    public void QuotationTemplateRendersAValidPdf()
    {
        using var provider = BuildProvider();
        var generator = provider.GetRequiredService<IPdfGenerator>();

        var file = generator.Generate(Model("INT/417/2026 rev01", "Delta Steel Co."));

        Assert.Equal("quotation-INT-417-2026-rev01.pdf", file.FileName);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(file.Content, 0, 5));
        Assert.True(file.Content.Length > 500);
        Assert.Equal("application/pdf", PdfFile.ContentType);
    }

    [Fact]
    public void TheQuotationHasTheTenPagesOfTheReferenceDesignAndStaysSmallEnoughToEmail()
    {
        using var provider = BuildProvider();

        var file = provider.GetRequiredService<IPdfGenerator>().Generate(Model("INT/417/2026 rev01", "Delta Steel Co."));

        Assert.Equal(10, PageCount(file.Content));
        Assert.True(file.Content.Length < 1_000_000, $"The PDF is {file.Content.Length} bytes.");
    }

    [Fact]
    public void ManyItemsAndNotesFlowOntoExtraPagesInsteadOfFailing()
    {
        using var provider = BuildProvider();
        var items = Enumerable.Range(1, 60)
            .Select(index => new QuotationPdfItem(
                $"Machine {index}", $"Pressure vessel number {index} - 2,000 L / 10 bar", index % 2 == 0 ? $"PR{index}" : null, index, index * 12_345, "Per Visit"))
            .ToArray();

        var file = provider.GetRequiredService<IPdfGenerator>().Generate(Model("INT/417/2026 rev01", "Delta Steel Co.") with
        {
            Items = items,
            Notes = Enumerable.Repeat("A long note that goes on and on to take some room on the page, repeated many times.", 30).ToArray()
        });

        Assert.True(PageCount(file.Content) > 10);
    }

    [Fact]
    public void VeryLongNamesDoNotBreakTheCover()
    {
        using var provider = BuildProvider();

        var file = provider.GetRequiredService<IPdfGenerator>().Generate(Model("INT/417/2026 rev01", new string('N', 400)) with
        {
            ContactName = new string('C', 300),
            Subject = string.Join(", ", Enumerable.Repeat("Mobile crane", 30)),
            PreparedBy = new string('P', 200)
        });

        Assert.Equal(10, PageCount(file.Content));
    }

    [Fact]
    public void FileNamesAreSafeForHeadersAndFileSystems()
    {
        using var provider = BuildProvider();
        var generator = provider.GetRequiredService<IPdfGenerator>();

        var file = generator.Generate(Model("../..\\evil\"name", "Client"));

        Assert.DoesNotContain('/', file.FileName);
        Assert.DoesNotContain('\\', file.FileName);
        Assert.DoesNotContain('"', file.FileName);
        Assert.EndsWith(".pdf", file.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public void TextNoFontCoversDoesNotBreakGeneration()
    {
        using var provider = BuildProvider();
        var generator = provider.GetRequiredService<IPdfGenerator>();

        var file = generator.Generate(Model("INT/1/2026 rev01", "شركة النور 🚀 Industries"));

        Assert.True(file.Content.Length > 0);
    }

    [Fact]
    public void ModelsWithoutATemplateFailWithAClearError()
    {
        using var provider = BuildProvider();
        var generator = provider.GetRequiredService<IPdfGenerator>();

        var exception = Assert.Throws<PdfGenerationException>(() => generator.Generate(new UnknownModel()));

        Assert.Contains(nameof(UnknownModel), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderingFailuresAreWrappedAndTheOriginalErrorIsKept()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPdfGeneration(Configuration());
        services.AddPdfTemplate<UnknownModel, ExplodingTemplate>();
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<PdfGenerationException>(
            () => provider.GetRequiredService<IPdfGenerator>().Generate(new UnknownModel()));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public void NewTemplatesPlugInThroughTheBaseClass()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPdfGeneration(Configuration());
        services.AddPdfTemplate<UnknownModel, MinimalTemplate>();
        using var provider = services.BuildServiceProvider();

        var file = provider.GetRequiredService<IPdfGenerator>().Generate(new UnknownModel());

        Assert.Equal("minimal.pdf", file.FileName);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(file.Content, 0, 5));
    }

    [Fact]
    public void UnknownLicenseNamesAreRejectedAtStartup()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddPdfGeneration(Configuration(("Pdf:QuestPdfLicense", "Freeware"))));
    }

    [Fact]
    public void CommunityIsTheDefaultLicense()
    {
        using var provider = BuildProvider();

        Assert.Equal(LicenseType.Community, QuestPDF.Settings.License);
    }

    // The page tree root carries the total in its "/Count N" entry; nested nodes carry smaller counts.
    private static int PageCount(byte[] pdf)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Count (\d+)");
        return matches.Count == 0 ? -1 : matches.Max(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static QuotationPdfModel Model(string proposalNumber, string clientName) => new(
        ProposalNumber: proposalNumber,
        QuotationNumber: "QT-2026-0142",
        ClientName: clientName,
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

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPdfGeneration(Configuration());
        return services.BuildServiceProvider();
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(item => item.Key, item => (string?)item.Value))
            .Build();

    private sealed record UnknownModel;

    private sealed class MinimalTemplate : QuestPdfTemplate<UnknownModel>
    {
        public override string FileName(UnknownModel model) => "minimal.pdf";
        protected override string Title(UnknownModel model) => "Minimal";
        protected override void ComposeContent(IContainer container, UnknownModel model) => container.Text("Hello");
    }

    private sealed class ExplodingTemplate : IPdfTemplate<UnknownModel>
    {
        public string FileName(UnknownModel model) => "boom.pdf";
        public byte[] Render(UnknownModel model) => throw new InvalidOperationException("boom");
    }
}
