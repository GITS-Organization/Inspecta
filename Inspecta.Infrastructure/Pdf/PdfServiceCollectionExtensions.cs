using Inspecta.Application.Pdf;
using Inspecta.Application.Pdf.Models;
using Inspecta.Infrastructure.Pdf.Templates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace Inspecta.Infrastructure.Pdf;

public static class PdfServiceCollectionExtensions
{
    /// <summary>
    /// Registers PDF generation and every template. Set <c>Pdf:QuestPdfLicense</c> to <c>Community</c>
    /// (default), <c>Professional</c> or <c>Enterprise</c> to match the QuestPDF license the company holds.
    /// </summary>
    public static IServiceCollection AddPdfGeneration(this IServiceCollection services, IConfiguration configuration)
    {
        var licenseName = configuration["Pdf:QuestPdfLicense"];
        var license = LicenseType.Community;
        if (!string.IsNullOrWhiteSpace(licenseName) &&
            (!Enum.TryParse(licenseName, ignoreCase: true, out license) || !Enum.IsDefined(license)))
            throw new InvalidOperationException("Pdf:QuestPdfLicense must be Community, Professional or Enterprise.");

        PdfFonts.Register();
        QuestPDF.Settings.License = license;
        // Client names and email-derived text can contain characters no font covers; draw a placeholder
        // glyph instead of failing the whole document.
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;

        services.AddSingleton<IPdfGenerator, PdfGenerator>();
        services.AddPdfTemplate<QuotationPdfModel, QuotationPdfTemplate>();
        services.AddPdfTemplate<CertificatePdfModel, CertificatePdfTemplate>();
        return services;
    }

    /// <summary>Registers the template used to render <typeparamref name="TModel"/>.</summary>
    public static IServiceCollection AddPdfTemplate<TModel, TTemplate>(this IServiceCollection services)
        where TTemplate : class, IPdfTemplate<TModel> =>
        services.AddSingleton<IPdfTemplate<TModel>, TTemplate>();
}
