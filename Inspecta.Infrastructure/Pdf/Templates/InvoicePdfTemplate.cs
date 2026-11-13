using System.Globalization;
using Inspecta.Application.Pdf.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Inspecta.Infrastructure.Pdf.Templates;

/// <summary>
/// The invoice sent to the client, laid out like the company's e-invoice form: title, issuer, recipient, the lines table and the totals.
/// It has no QR code, status or electronic id: the official e-invoice is made in another system.
/// </summary>
public sealed class InvoicePdfTemplate : QuestPdfTemplate<InvoicePdfModel>
{
    private static readonly Color Slate = Color.FromHex("#3B4B5C");

    public override string FileName(InvoicePdfModel model) => $"invoice-{SafeFileName(model.Number)}.pdf";

    protected override string Title(InvoicePdfModel model) => $"Invoice {model.Number}";

    protected override void ComposeHeader(IContainer container, InvoicePdfModel model) =>
        container.Row(row =>
        {
            row.RelativeItem().Column(title =>
            {
                title.Item().Text("Invoice").Bold().FontSize(22).FontColor(PdfTheme.Navy);
                title.Item().PaddingTop(2).Text($"No. {model.Number}").FontSize(10).FontColor(PdfTheme.Muted);
                title.Item().Text($"Date: {model.IssuedOn.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}").FontSize(10).FontColor(PdfTheme.Muted);
            });
            if ((model.Branding?.LogoPng ?? PdfAssets.Logo) is { Length: > 0 } logo)
                row.ConstantItem(90).Height(60).AlignRight().Image(logo).FitArea();
        });

    protected override void ComposeContent(IContainer container, InvoicePdfModel model)
    {
        var fallback = DefaultCompanyProfile.Instance;
        container.PaddingTop(14).Column(column =>
        {
            column.Spacing(10);
            column.Item().Element(box => Party(
                box, "Issuer (From)",
                [
                    ("Taxpayer Name", model.IssuerName ?? fallback.Name),
                    ("Registration Number", model.IssuerTaxRegistrationNumber),
                    ("Taxpayer Activity Code", model.IssuerActivityCode),
                    ("Address", model.IssuerAddress ?? fallback.Address)
                ]));
            if (model.QuotationReference is not null)
                column.Item().Text(text =>
                {
                    text.Span("Quotation reference: ").SemiBold().FontColor(PdfTheme.Muted);
                    text.Span(model.QuotationReference);
                });
            column.Item().Element(box => Party(
                box, "Recipient (To)",
                [
                    ("Taxpayer Name", model.ClientName),
                    ("Registration Number", model.ClientTaxNumber),
                    ("Address", model.ClientAddress)
                ]));
            column.Item().PaddingTop(4).Element(table => Lines(table, model));
            column.Item().AlignRight().Width(300).Element(totals => Totals(totals, model));
        });
    }

    private static void Party(IContainer container, string title, IReadOnlyList<(string Label, string? Value)> fields) =>
        container.Border(0.7f).BorderColor(PdfTheme.Line).Column(column =>
        {
            column.Item().Background(Slate).Padding(5).Text(title).Bold().FontSize(10).FontColor(PdfTheme.White);
            column.Item().Padding(6).Column(body =>
            {
                body.Spacing(2);
                foreach (var (label, value) in fields.Where(field => !string.IsNullOrWhiteSpace(field.Value)))
                    body.Item().Text(text =>
                    {
                        text.Span($"{label}: ").SemiBold().FontColor(PdfTheme.Muted).FontSize(9);
                        text.Span(value!).FontSize(9.5f);
                    });
            });
        });

    private static void Lines(IContainer container, InvoicePdfModel model) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(3.4f);
                columns.RelativeColumn(1.7f);
                columns.RelativeColumn(1.8f);
                columns.RelativeColumn(2f);
            });
            table.Header(header =>
            {
                foreach (var label in new[] { "Code Name", "Description", "Quantity / Unit Type", "Unit Price (EGP)", "Total Sales Amount (EGP)" })
                    header.Cell().Background(Slate).Padding(5).Text(label).Bold().FontSize(8.5f).FontColor(PdfTheme.White);
            });
            foreach (var line in model.Lines)
            {
                Cell(table, line.CodeName, bold: true);
                Cell(table, line.Description);
                Cell(table, $"{line.Quantity.ToString(CultureInfo.InvariantCulture)} / {line.UnitType}");
                Cell(table, line.UnitPricePiastres is long price ? Egp(price) : "-", alignRight: true);
                Cell(table, Egp(line.TotalPiastres), alignRight: true);
            }
        });

    private static void Cell(TableDescriptor table, string text, bool bold = false, bool alignRight = false)
    {
        var cell = table.Cell().BorderBottom(0.7f).BorderColor(PdfTheme.Line).Padding(5);
        var aligned = alignRight ? cell.AlignRight() : cell;
        var span = aligned.Text(text).FontSize(9);
        if (bold) span.SemiBold();
    }

    private static void Totals(IContainer container, InvoicePdfModel model) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2f);
                columns.RelativeColumn(1.4f);
            });
            Row(table, "Total Sales (EGP)", Egp(model.SubtotalPiastres));
            Row(table, model.DiscountPercent > 0 ? $"Total Discount ({Percent(model.DiscountPercent)}%) (EGP)" : "Total Discount (EGP)", Egp(model.DiscountPiastres));
            Row(table, model.VatPercent > 0 ? $"Value Added Tax ({Percent(model.VatPercent)}%) (EGP)" : "Value Added Tax (EGP)", Egp(model.VatPiastres));
            Row(table, "Total Amount (EGP)", Egp(model.TotalPiastres), strong: true);
            Row(table, "Paid (EGP)", Egp(model.PaidPiastres));
            Row(table, "Balance (EGP)", Egp(model.BalancePiastres), strong: true);
        });

    private static void Row(TableDescriptor table, string label, string value, bool strong = false)
    {
        var fill = strong ? PdfTheme.Band : PdfTheme.White;
        var left = table.Cell().Background(fill).BorderBottom(0.7f).BorderColor(PdfTheme.Line).Padding(5).Text(label).FontSize(9);
        var right = table.Cell().Background(fill).BorderBottom(0.7f).BorderColor(PdfTheme.Line).Padding(5).AlignRight().Text(value).FontSize(9);
        if (strong)
        {
            left.Bold();
            right.Bold();
        }
    }

    private static string Egp(long piastres) => (piastres / 100m).ToString("N2", CultureInfo.InvariantCulture);

    private static string Percent(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string SafeFileName(string value)
    {
        var cleaned = new string(value.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-').ToArray()).Trim('-');
        return cleaned.Length == 0 ? "invoice" : cleaned;
    }
}
