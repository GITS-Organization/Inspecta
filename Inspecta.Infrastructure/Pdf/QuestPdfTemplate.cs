using Inspecta.Application.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Inspecta.Infrastructure.Pdf;

/// <summary>
/// Base class for PDF templates. A new document is a model record in the application layer plus a subclass
/// that implements <see cref="FileName"/> and <see cref="Title"/> and either <see cref="ComposeContent"/> (one
/// flowing page with header and footer) or <see cref="ComposeDocument"/> (full control over the pages). Register it with
/// <see cref="PdfServiceCollectionExtensions.AddPdfTemplate{TModel, TTemplate}"/>.
/// </summary>
public abstract class QuestPdfTemplate<TModel> : IPdfTemplate<TModel>
{
    public abstract string FileName(TModel model);

    /// <summary>Document title stored in the PDF metadata.</summary>
    protected abstract string Title(TModel model);

    /// <summary>Header drawn at the top of every page. Empty by default.</summary>
    protected virtual void ComposeHeader(IContainer container, TModel model) { }

    /// <summary>Body of the document; pages break automatically when the content is long.</summary>
    protected virtual void ComposeContent(IContainer container, TModel model) { }

    /// <summary>Footer drawn at the bottom of every page. Shows the page number by default.</summary>
    protected virtual void ComposeFooter(IContainer container, TModel model) =>
        container.AlignCenter().Text(text =>
        {
            text.DefaultTextStyle(style => style.FontSize(8).FontColor(PdfTheme.Muted));
            text.Span("Page ");
            text.CurrentPageNumber();
            text.Span(" of ");
            text.TotalPages();
        });

    /// <summary>Adds the pages. By default one A4 page with <see cref="ComposeHeader"/>, content and footer.</summary>
    protected virtual void ComposeDocument(IDocumentContainer document, TModel model) =>
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(PdfTheme.PageMargin);
            page.DefaultTextStyle(style => style
                .FontFamily(PdfTheme.FontFamily)
                .FontSize(PdfTheme.BodyFontSize)
                .FontColor(PdfTheme.Navy));
            page.Header().Element(container => ComposeHeader(container, model));
            page.Content().Element(container => ComposeContent(container, model));
            page.Footer().Element(container => ComposeFooter(container, model));
        });

    public byte[] Render(TModel model) =>
        Document.Create(document => ComposeDocument(document, model))
            .WithSettings(new DocumentSettings
            {
                // The cover illustration is a large raster image; these keep the emailed PDF small.
                ImageCompressionQuality = ImageCompressionQuality.Medium,
                ImageRasterDpi = 144
            })
            .WithMetadata(new DocumentMetadata
            {
                Title = Title(model),
                Author = "Inspecta",
                Creator = "Inspecta",
                Producer = "Inspecta"
            })
            .GeneratePdf();
}
