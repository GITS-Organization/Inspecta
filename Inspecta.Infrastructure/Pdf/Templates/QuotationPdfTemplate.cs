using System.Globalization;
using Inspecta.Application.Pdf.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Inspecta.Infrastructure.Pdf.Templates;

/// <summary>
/// Technical and commercial offer. The content is a rough first version used to test the data end to end;
/// the final design of each section replaces it later.
/// </summary>
public sealed class QuotationPdfTemplate : QuestPdfTemplate<QuotationPdfModel>
{
    public override string FileName(QuotationPdfModel model) =>
        $"quotation-{SafeFileName(model.ProposalNumber)}.pdf";

    protected override string Title(QuotationPdfModel model) =>
        $"Technical and commercial offer {model.ProposalNumber}";

    protected override void ComposeHeader(IContainer container, QuotationPdfModel model) =>
        container.PaddingBottom(12).BorderBottom(1).BorderColor(PdfTheme.Line).Row(row =>
        {
            row.RelativeItem().AlignMiddle().Text("TECHNICAL AND COMMERCIAL OFFER").Bold().FontSize(12);
            row.ConstantItem(190).Column(column =>
            {
                column.Item().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(9));
                    text.Span("Date  ").FontColor(PdfTheme.Muted);
                    text.Span(FormatDate(model.IssuedAt));
                });
                column.Item().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(9));
                    text.Span("Proposal No.  ").FontColor(PdfTheme.Muted);
                    text.Span(model.ProposalNumber);
                });
                column.Item().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(9));
                    text.Span("Page  ").FontColor(PdfTheme.Muted);
                    text.CurrentPageNumber();
                });
            });
        });

    protected override void ComposeContent(IContainer container, QuotationPdfModel model) =>
        container.PaddingVertical(14).Column(column =>
        {
            column.Spacing(14);

            column.Item().Column(intro =>
            {
                intro.Spacing(4);
                intro.Item().Text($"Offer for {model.ClientName}").Bold().FontSize(15);
                intro.Item().Text($"Valid until {FormatDate(model.ValidUntil)}").FontColor(PdfTheme.Muted);
                if (!string.IsNullOrWhiteSpace(model.Message))
                    intro.Item().PaddingTop(6).Text(model.Message);
            });

            column.Item().Element(content => ComposeScope(content, model));
            column.Item().Element(content => ComposeBudget(content, model));

            if (model.Notes.Count > 0)
                column.Item().Element(content => ComposeList(content, "Notes", model.Notes));
        });

    private static void ComposeScope(IContainer container, QuotationPdfModel model) =>
        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Text("Scope of work").Bold().FontSize(12);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(24);
                    columns.ConstantColumn(70);
                    columns.RelativeColumn();
                    columns.ConstantColumn(40);
                });
                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "#");
                    HeaderCell(header.Cell(), "PR number");
                    HeaderCell(header.Cell(), "Description");
                    HeaderCell(header.Cell(), "Qty");
                });
                for (var index = 0; index < model.Items.Count; index++)
                {
                    var item = model.Items[index];
                    BodyCell(table.Cell(), (index + 1).ToString(CultureInfo.InvariantCulture));
                    BodyCell(table.Cell(), item.PrNumber ?? "--");
                    BodyCell(table.Cell(), item.Description);
                    BodyCell(table.Cell(), item.Quantity.ToString(CultureInfo.InvariantCulture));
                }
            });

            if (model.ScopeOfWork.Count > 0)
                column.Item().PaddingTop(6).Element(content => ComposeList(content, "The visit covers", model.ScopeOfWork));
        });

    private static void ComposeBudget(IContainer container, QuotationPdfModel model) =>
        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Text("Project budget").Bold().FontSize(12);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(24);
                    columns.RelativeColumn();
                    columns.ConstantColumn(60);
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(30);
                    columns.ConstantColumn(85);
                });
                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "#");
                    HeaderCell(header.Cell(), "Description");
                    HeaderCell(header.Cell(), "Pricing");
                    HeaderCell(header.Cell(), "Cost per item");
                    HeaderCell(header.Cell(), "Qty");
                    HeaderCell(header.Cell(), "Total");
                });
                for (var index = 0; index < model.Items.Count; index++)
                {
                    var item = model.Items[index];
                    BodyCell(table.Cell(), (index + 1).ToString(CultureInfo.InvariantCulture));
                    BodyCell(table.Cell(), item.Description);
                    BodyCell(table.Cell(), item.PricingUnit);
                    BodyCell(table.Cell(), FormatMoney(item.UnitPricePiastres));
                    BodyCell(table.Cell(), item.Quantity.ToString(CultureInfo.InvariantCulture));
                    BodyCell(table.Cell(), FormatMoney(item.TotalPiastres));
                }
            });

            var total = model.Items.Aggregate(0L, (sum, item) => checked(sum + (item.TotalPiastres ?? 0)));
            column.Item().AlignRight().Text($"Total {FormatMoney(total)}").Bold();
            column.Item().AlignRight().Text("VAT will be added.").FontColor(PdfTheme.Muted).FontSize(9);
        });

    private static void ComposeList(IContainer container, string title, IReadOnlyList<string> points) =>
        container.Column(column =>
        {
            column.Spacing(3);
            column.Item().Text(title).Bold();
            for (var index = 0; index < points.Count; index++)
            {
                var number = (index + 1).ToString(CultureInfo.InvariantCulture);
                column.Item().Row(row =>
                {
                    row.ConstantItem(20).Text(number).FontColor(PdfTheme.Muted);
                    row.RelativeItem().Text(points[index]);
                });
            }
        });

    private static void HeaderCell(IContainer cell, string text) =>
        cell.BorderBottom(1).BorderColor(PdfTheme.Primary).PaddingVertical(4).PaddingRight(4)
            .Text(text).Bold().FontSize(9);

    private static void BodyCell(IContainer cell, string text) =>
        cell.BorderBottom(1).BorderColor(PdfTheme.Line).PaddingVertical(4).PaddingRight(4).Text(text);

    private static string FormatDate(DateTimeOffset value) =>
        value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    // Prices are stored in piastres; an item without a price yet prints as "--".
    private static string FormatMoney(long? piastres) =>
        piastres is long value
            ? (value / 100m).ToString("N2", CultureInfo.InvariantCulture) + " EGP"
            : "--";

    private static string SafeFileName(string value)
    {
        var cleaned = new string(value.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-').ToArray()).Trim('-');
        return cleaned.Length == 0 ? "offer" : cleaned;
    }
}
