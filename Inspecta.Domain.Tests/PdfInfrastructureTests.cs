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

    [Theory]
    [InlineData(10)]
    [InlineData(12.5)]
    [InlineData(100)]
    public void ADiscountedQuotationRendersAValidPdfWithTheSamePageCount(double percent)
    {
        using var provider = BuildProvider();
        var generator = provider.GetRequiredService<IPdfGenerator>();
        var plain = generator.Generate(Model("INT/417/2026 rev01", "Delta Steel Co."));

        var discounted = generator.Generate(Model("INT/417/2026 rev01", "Delta Steel Co.") with { DiscountPercent = (decimal)percent });

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(discounted.Content, 0, 5));
        Assert.Equal(PageCount(plain.Content), PageCount(discounted.Content));
        Assert.NotEqual(plain.Content.Length, discounted.Content.Length);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(5.5)]
    public void TheVatChangesThePdfAndKeepsItAValidDocument(double percent)
    {
        using var provider = BuildProvider();
        var generator = provider.GetRequiredService<IPdfGenerator>();
        var none = generator.Generate(Model("INT/417/2026 rev01", "Delta Steel Co.") with { VatPercent = 0m });

        var withVat = generator.Generate(Model("INT/417/2026 rev01", "Delta Steel Co.") with { VatPercent = (decimal)percent });

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(withVat.Content, 0, 5));
        Assert.NotEqual(none.Content.Length, withVat.Content.Length);
        Assert.Equal(PageCount(none.Content), PageCount(withVat.Content));
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ACertificateWithItsBackgroundStaysSmall(bool draft)
    {
        using var provider = BuildProvider();
        var model = new CertificatePdfModel(
            draft ? null : "CERT-2026-0001", DateTimeOffset.UtcNow, draft, "Delta Steel Co.", "10th of Ramadan", "Ahmed",
            null, null, null, new DateOnly(2026, 10, 26), new DateOnly(2027, 10, 25), "Forklift Truck", ["ISO 2330"], "Visual",
            "Satisfactory", [new CertificatePdfField("Serial Number", "FK-100")],
            new CertificatePdfQuestionnaire(true, true, false, true, true, false, "NONE", false, false, null, "N/A", "NONE", null),
            [new CertificatePdfSection("Brakes", [new CertificatePdfRow("Service brake", "Yes", null)])],
            "Karim", "Lifts", "Mona", null, "https://example.com/verify/ABCD");

        var file = provider.GetRequiredService<IPdfGenerator>().Generate(model);

        // The faded logo is one image stored once: the file must not grow with it (a repeated logo made it 2 MB).
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(file.Content, 0, 5));
        Assert.InRange(file.Content.Length, 10_000, 600_000);
    }

    [Fact]
    public void AGearCertificateFilledToItsPageBudgetIsOnePage()
    {
        using var provider = BuildProvider();
        var serials = string.Join(", ", Enumerable.Range(1, 400).Select(number => "CS-26-" + number.ToString("0000")))[..Inspecta.Application.Certificates.SerialNumbers.MaxCharacters];
        // The page budget: the serial text and the four free texts share it (1000 + 4 x 50 = 1200 characters).
        var longText = string.Join(' ', Enumerable.Repeat("The sling shows wear near the hook and must be replaced.", 2))[..50];
        var model = new CertificatePdfModel(
            "CERT-2026-0001", DateTimeOffset.UtcNow, false, "Delta Steel Co.",
            "10th of Ramadan - Zone B3", "Ahmed Rafat", null, null, null,
            new DateOnly(2026, 10, 26), new DateOnly(2027, 10, 25), "Full Body Safety Harness", ["BS EN 361:2002"], "Visual & function test",
            "Unsatisfactory",
            [new CertificatePdfField("Description", "Full Body Safety Harness"), new CertificatePdfField("SWL", "25 tonnes"),
                new CertificatePdfField("Qty.", "1000"), new CertificatePdfField("Serial Number", serials)],
            new CertificatePdfQuestionnaire(false, null, true, false, true, false, longText, true, true, new DateOnly(2026, 12, 1), longText, longText, longText),
            [], "Karim Adel", "Lifts, Lifting equipment, Pressure vessels", "Mona Manager", null, "https://example.com/verify/ABCD");

        var file = provider.GetRequiredService<IPdfGenerator>().Generate(model);

        var pages = System.Text.RegularExpressions.Regex.Matches(Encoding.Latin1.GetString(file.Content), @"/Type\s*/Page(?![s\w])").Count;
        Assert.Equal(1, pages);
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

    private sealed class SlowTemplate : IPdfTemplate<UnknownModel>
    {
        public static int Running;
        public static int MostAtOnce;

        public string FileName(UnknownModel model) => "slow.pdf";

        public byte[] Render(UnknownModel model)
        {
            var now = Interlocked.Increment(ref Running);
            lock (typeof(SlowTemplate)) MostAtOnce = Math.Max(MostAtOnce, now);
            Thread.Sleep(80);
            Interlocked.Decrement(ref Running);
            return [37, 80, 68, 70];
        }
    }

    [Fact]
    public void PdfRenderingNeverExceedsThePermitCountAndEveryDocumentIsStillDrawn()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPdfGeneration(Configuration());
        services.AddPdfTemplate<UnknownModel, SlowTemplate>();
        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IPdfGenerator>();
        SlowTemplate.Running = 0;
        SlowTemplate.MostAtOnce = 0;

        var files = Enumerable.Range(0, 8).Select(_ => Task.Run(() => generator.Generate(new UnknownModel()))).ToArray();
        Task.WaitAll(files);

        Assert.Equal(8, files.Count(task => task.Result.Content.Length == 4));
        Assert.InRange(SlowTemplate.MostAtOnce, 1, Inspecta.Infrastructure.Pdf.PdfGenerator.MaxConcurrentRenders);
    }

    private sealed class ExplodingTemplate : IPdfTemplate<UnknownModel>
    {
        public string FileName(UnknownModel model) => "boom.pdf";
        public byte[] Render(UnknownModel model) => throw new InvalidOperationException("boom");
    }
}
