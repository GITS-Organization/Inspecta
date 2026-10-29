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

    private static QuotationPdfModel Model(string proposalNumber, string clientName) => new(
        proposalNumber,
        clientName,
        new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero),
        "Please find our offer below.",
        [
            new QuotationPdfItem("Mobile crane - Liebherr LTM 1100, 100 t", "13351021", 1, 1_200_000, "Per Visit"),
            new QuotationPdfItem("Air receiver - 3000 L", null, 2, null, "Per Visit")
        ],
        ["Load and functional test", "Visual inspection"],
        ["Pricing excludes VAT."]);

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
