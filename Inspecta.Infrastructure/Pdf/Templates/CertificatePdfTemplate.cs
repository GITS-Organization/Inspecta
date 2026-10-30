using System.Globalization;
using System.Text;
using System.Text.Json;
using Inspecta.Application.Pdf.Models;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Inspecta.Infrastructure.Pdf.Templates;

/// <summary>
/// The Report of Thorough Examination of one machine. One layout serves every machine: the technical fields and the
/// checklist come from the machine's template, the rest (questionnaire, notes, signatures, QR) is the same for all.
/// </summary>
public sealed class CertificatePdfTemplate : QuestPdfTemplate<CertificatePdfModel>
{
    private static readonly Color Ink = Colors.Black;
    private static readonly Color TitleRed = Color.FromHex("#E01B24");
    private static readonly Color Grey = Color.FromHex("#E3E3E3");
    private static readonly Color Green = Color.FromHex("#2E9B3E");

    private const float Line = 0.7f;
    private const float Pad = 3f;
    private const string CompanySigner = "Ahmed Khalil";

    private static readonly string[] GeneralNotes =
    [
        "This Certificate becomes invalid if any Repair/modification is done that affects strength or stability.",
        "Any holder of this document is advised that the information contained hereon is limited to the accessible portions of the item examined only.",
        "Equipment has been correctly installed and fit for the purpose",
        "This Certificate should be signed, stamped and carries unique Integrity for inspection and Engineering Solutions certificate number"
    ];

    private const string TickSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"11\" height=\"11\" viewBox=\"0 0 12 12\"><path d=\"M1.5 6.5 L4.8 10 L10.5 1.8\" fill=\"none\" stroke=\"#111111\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg>";

    public override string FileName(CertificatePdfModel model) =>
        $"certificate-{SafeFileName(model.CertificateNumber ?? model.ItemName)}.pdf";

    protected override string Title(CertificatePdfModel model) =>
        $"Report of Thorough Examination {model.CertificateNumber ?? "(draft)"} - {model.ItemName}";

    protected override void ComposeDocument(IDocumentContainer document, CertificatePdfModel model) =>
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(20);
            page.DefaultTextStyle(style => style.FontFamily(PdfTheme.FontFamily).FontSize(8.4f).FontColor(Ink));
            // The frame around every page.
            page.Background().Border(1).BorderColor(Ink);
            page.Header().PaddingHorizontal(10).PaddingTop(8).Element(container => ComposeHeader(container, model));
            page.Content().PaddingHorizontal(10).PaddingVertical(6).Element(container => ComposeBody(container, model));
            page.Footer().PaddingHorizontal(10).PaddingBottom(6).Element(container => ComposeFooter(container, model));
            if (model.IsDraft)
                page.Foreground().AlignCenter().AlignMiddle().Rotate(-35)
                    .Text("DRAFT").FontSize(110).Bold().FontColor(Color.FromRGB(190, 197, 210).WithAlpha(90));
        });

    // ---- Header and footer --------------------------------------------------------------------------------------

    protected override void ComposeHeader(IContainer container, CertificatePdfModel model) =>
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(lines =>
                {
                    lines.Item().Text("Integrity International Inspection.").Bold();
                    lines.Item().Text("Industrial & Energy Services.");
                    lines.Item().Text("21 Nakib Sharif Ramzy st, Smouha, Alex., Egypt.");
                    lines.Item().Text(text =>
                    {
                        text.Span("Mobile No: ").Bold();
                        text.Span("+201000914066 - 01034462363").Bold().FontColor(Green);
                    });
                    lines.Item().Text(text =>
                    {
                        text.Span("Email: ").Bold();
                        text.Span("Ahmed.khalil@integrity-intl.com").Underline().FontColor(Color.FromHex("#1565C0"));
                    });
                    lines.Item().Text(text =>
                    {
                        text.Span("Email: ").Bold();
                        text.Span("Ibrahim.ashraf@integrity-intl.com").Underline().FontColor(Color.FromHex("#1565C0"));
                    });
                });
                row.ConstantItem(95).AlignRight().Height(54).Image(PdfAssets.Logo).FitArea();
            });
            column.Item().PaddingTop(3).AlignCenter().Text("REPORT OF THOROUGH EXAMINATION").Bold().FontSize(13.5f).FontColor(TitleRed);
            column.Item().Height(3);
        });

    protected override void ComposeFooter(IContainer container, CertificatePdfModel model) =>
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignBottom().Column(lines =>
                {
                    lines.Item().Text("Integrity International Inspection. All rights reserved.").Bold().FontSize(6.8f);
                    lines.Item().Text("Integrity International Inspection | 21 Nakib Sharif Ramzy St., Smouha, Alexandria, Egypt").Bold().FontSize(6.8f);
                    lines.Item().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(6.8f).Bold());
                        text.Span("Tel: ").FontColor(Green);
                        text.Span("+2 01034462363 | +20 100 091 4066").FontColor(Green);
                    });
                    lines.Item().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(6.8f).Bold());
                        text.Span("Email: ").FontColor(Color.FromHex("#1565C0"));
                        text.Span("Ahmed.khalil@integrity-intl.com / Info@integrity-intl.com").FontColor(Color.FromHex("#1565C0"));
                    });
                    lines.Item().Text("This Certificate is confidential and intended solely for the use of the client for whom it was prepared.").Bold().FontSize(6.8f);
                    lines.Item().Text("Unauthorized use, reproduction, or distribution is strictly prohibited.").Bold().FontSize(6.8f);
                });
                row.ConstantItem(150).AlignRight().AlignBottom().Height(46).Image(PdfAssets.Accreditations).FitArea();
            });
            column.Item().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(6.5f).FontColor(PdfTheme.Muted));
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });

    // ---- Body ---------------------------------------------------------------------------------------------------

    private static void ComposeBody(IContainer container, CertificatePdfModel model) =>
        container.Column(column =>
        {
            column.Item().Element(content => ComposeTop(content, model));
            column.Item().Element(content => ComposeItem(content, model));
            column.Item().Element(content => ComposeQuestionnaire(content, model.Questionnaire));
            column.Item().Element(ComposeGeneralNotes);
            column.Item().Element(content => ComposeSignatures(content, model));
            if (model.Checklist.Count > 0)
            {
                column.Item().PageBreak();
                column.Item().PaddingBottom(4).Text($"Inspection checklist - {model.ItemName}").Bold().FontSize(11);
                foreach (var section in model.Checklist)
                    column.Item().PaddingBottom(5).Element(content => ComposeChecklistSection(content, section));
            }
        });

    private static void ComposeTop(IContainer container, CertificatePdfModel model) =>
        container.Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3.4f);
                    columns.RelativeColumn(2.1f);
                    columns.RelativeColumn(2.1f);
                    columns.RelativeColumn(1.9f);
                });
                Head(table, "Client and Location of thorough examination");
                Head(table, "Client representative");
                Head(table, "Certificate number");
                Head(table, "Certificate date");
                table.Cell().Element(Cell).AlignCenter().Column(lines =>
                {
                    lines.Item().AlignCenter().Text(model.ClientName).Bold();
                    lines.Item().AlignCenter().Text(model.Location).FontSize(7.8f);
                });
                Value(table, model.ClientRepresentative, bold: true);
                Value(table, model.CertificateNumber ?? "(on approval)", bold: true);
                Value(table, model.CertificateDate is { } issued ? Format(issued) : null, bold: true);
            });
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(3.3f);
                    columns.RelativeColumn(1.9f);
                });
                Head(table, "Through examination date");
                Head(table, "Next examination date");
                Head(table, "Standard of inspection");
                Head(table, "Test type");
                Value(table, model.ExaminationDate is { } exam ? Format(exam) : null, bold: true);
                Value(table, model.NextExaminationDate is { } next ? Format(next) : null, bold: true);
                table.Cell().Element(Cell).AlignCenter().AlignMiddle().Column(lines =>
                {
                    if (model.Standards.Count == 0) lines.Item().AlignCenter().Text("—");
                    foreach (var standard in model.Standards) lines.Item().AlignCenter().Text(standard);
                });
                Value(table, model.TestType, bold: true);
            });
        });

    private static void ComposeItem(IContainer container, CertificatePdfModel model) =>
        container.Column(column =>
        {
            column.Item().Element(Cell).AlignCenter().Text("Item information / Test results.").Bold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.8f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.8f);
                });
                var pairs = new List<(string Label, string? Value)> { ("Inspected Item:", model.ItemName) };
                pairs.AddRange(model.Fields.Select(field => (field.Label.TrimEnd(':') + ":", field.Value)));
                foreach (var (label, value) in pairs)
                {
                    table.Cell().Element(Cell).Text(label).Bold();
                    table.Cell().Element(Cell).Text(string.IsNullOrWhiteSpace(value) ? "—" : value);
                }

                // Complete the last row so the grid keeps its border.
                for (var filler = pairs.Count % 2 == 0 ? 0 : 2; filler > 0; filler--)
                    table.Cell().Element(Cell).Text(string.Empty);
                table.Cell().ColumnSpan(2).Element(c => GreyCell(c)).Text("Conclusion / remarks:").Bold();
                table.Cell().ColumnSpan(2).Element(c => GreyCell(c)).AlignCenter().Text(model.Conclusion).Bold();
            });
        });

    private static void ComposeQuestionnaire(IContainer container, CertificatePdfQuestionnaire q) =>
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(Cell).Column(left =>
                {
                    left.Item().Element(content => YesNo(content, "Is this the first examination after installation or assembly at a new site or location?", q.FirstExamination));
                    left.Item().PaddingTop(6).Element(content => YesNo(content, "If the answer to the above question is YES has the equipment been installed correctly?",
                        q.FirstExamination == true ? q.InstalledCorrectly : null));
                });
                row.RelativeItem().Element(Cell).Column(right =>
                {
                    right.Item().Text("Was the examination carried out:");
                    right.Item().PaddingTop(2).Element(content => YesNo(content, "Within an interval of 6 months?", q.Within6Months));
                    right.Item().PaddingTop(2).Element(content => YesNo(content, "Within an interval of 12 months?", q.Within12Months));
                    right.Item().PaddingTop(2).Element(content => YesNo(content, "In accordance with an examination scheme?", q.ExaminationScheme));
                    right.Item().PaddingTop(2).Element(content => YesNo(content, "After the occurrence of exceptional circumstances?", q.ExceptionalCircumstances));
                });
            });
            column.Item().Element(Cell).Text($"Identification of any part found to have a defect which is or could become a danger to persons and a description of the defect: {q.DefectDescription}");
            column.Item().Element(Cell).Element(content => YesNo(content, "Is the above an existing or imminent danger to persons *Note-This is a reportable defect", q.ExistingDanger));
            column.Item().Row(row =>
            {
                row.RelativeItem(3).Element(Cell).Text("Is the above a defect which is not yet but could become a danger to persons:(If YES state the date by when)");
                row.RelativeItem(2).Element(Cell).Text($"YES by:  {(q.FutureDanger == true && q.FutureDangerBy is { } by ? Format(by) : "N/A")}");
            });
            column.Item().Element(Cell).Text($"Particulars of any repair, renewal or alteration required to remedy the defect identified above: {q.RepairsRequired}");
            column.Item().Element(Cell).Text($"Particulars of any tests carried out as part of the examination: {q.TestsCarriedOut}");
            column.Item().Element(Cell).MinHeight(24).Text($"Note: {q.Note}");
        });

    private static void ComposeGeneralNotes(IContainer container) =>
        container.Row(row =>
        {
            row.ConstantItem(105).Element(Cell).AlignMiddle().AlignCenter().Text("General Notes and Recommendations").AlignCenter();
            row.RelativeItem().Element(Cell).Column(column =>
            {
                foreach (var note in GeneralNotes)
                    column.Item().Row(line =>
                    {
                        line.ConstantItem(9).Text("•");
                        line.RelativeItem().Text(note).FontSize(7.6f);
                    });
            });
        });

    private static void ComposeSignatures(IContainer container, CertificatePdfModel model) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3.1f);
                columns.RelativeColumn(3.8f);
                columns.RelativeColumn(2.4f);
                columns.RelativeColumn(1.9f);
            });
            Head(table, "Name & Qualifications of person making this report");
            Head(table, "Name of person signing or authenticating this report on behalf of the author");
            Head(table, "For Integrity for inspection and Engineering Solutions");
            Head(table, "QR Code");

            table.Cell().Element(Cell).Height(68).AlignMiddle().AlignCenter().Column(column =>
            {
                column.Item().AlignCenter().Text(model.PreparedByName ?? "—").Bold();
                if (!string.IsNullOrWhiteSpace(model.PreparedByQualifications))
                    column.Item().AlignCenter().Text(model.PreparedByQualifications).FontSize(7.4f);
            });
            table.Cell().Element(Cell).Height(68).Column(column =>
            {
                if (!string.IsNullOrWhiteSpace(model.SignatureStrokesJson))
                    column.Item().Height(42).Svg(size => SignatureSvg(model.SignatureStrokesJson, size.Width, size.Height));
                else column.Item().Height(42);
                column.Item().AlignCenter().Text(model.SignedBy ?? "—").Bold();
            });
            table.Cell().Element(Cell).Height(68).AlignMiddle().AlignCenter().Text(CompanySigner).Bold();
            table.Cell().Element(Cell).Height(68).AlignMiddle().AlignCenter().Element(qr =>
            {
                if (string.IsNullOrWhiteSpace(model.QrData)) return;
                qr.Width(58).Height(58).Image(QrPng(model.QrData)).FitArea();
            });
        });

    private static void ComposeChecklistSection(IContainer container, CertificatePdfSection section) =>
        container.ShowEntire().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(34);
                columns.RelativeColumn();
                columns.ConstantColumn(34);
            });
            table.Cell().ColumnSpan(4).Element(c => GreyCell(c)).Text(section.Title).Bold();

            var left = (section.Rows.Count + 1) / 2;
            for (var index = 0; index < left; index++)
            {
                ChecklistCells(table, section.Rows[index]);
                if (index + left < section.Rows.Count) ChecklistCells(table, section.Rows[index + left]);
                else
                {
                    table.Cell().Element(Cell).Text(string.Empty);
                    table.Cell().Element(Cell).Text(string.Empty);
                }
            }
        });

    private static void ChecklistCells(TableDescriptor table, CertificatePdfRow row)
    {
        table.Cell().Element(Cell).Text(text =>
        {
            text.Span(row.Label).FontSize(7.8f);
            if (!string.IsNullOrWhiteSpace(row.Note))
                text.Span($"  ({row.Note})").FontSize(7).Italic().FontColor(PdfTheme.Muted);
        });
        var failed = row.Value == "No";
        table.Cell().Element(c => TintedCell(c, failed ? Color.FromHex("#FBE3E1") : Colors.White)).AlignCenter()
            .Text(row.Value ?? "—").FontSize(7.8f).Bold();
    }

    // ---- Small building blocks ----------------------------------------------------------------------------------

    private static IContainer Cell(IContainer container) =>
        container.Border(Line).BorderColor(Ink).Padding(Pad);

    // The fill goes behind the padding too, so the whole cell is coloured.
    private static IContainer TintedCell(IContainer container, Color fill) =>
        container.Border(Line).BorderColor(Ink).Background(fill).Padding(Pad);

    private static IContainer GreyCell(IContainer container) => TintedCell(container, Grey);

    private static void Head(TableDescriptor table, string label) =>
        table.Cell().Element(Cell).AlignCenter().AlignMiddle().Text(label).Bold().AlignCenter();

    private static void Value(TableDescriptor table, string? value, bool bold = false)
    {
        var text = table.Cell().Element(Cell).AlignCenter().AlignMiddle().Text(string.IsNullOrWhiteSpace(value) ? "—" : value).AlignCenter();
        if (bold) text.Bold();
    }

    /// <summary>A question with YES and NO boxes; the chosen one carries a tick.</summary>
    private static void YesNo(IContainer container, string question, bool? answer) =>
        container.Row(row =>
        {
            row.RelativeItem().AlignMiddle().Text(question);
            row.ConstantItem(86).Row(boxes =>
            {
                AnswerBox(boxes, "YES", answer == true);
                AnswerBox(boxes, "NO", answer == false);
            });
        });

    private static void AnswerBox(RowDescriptor boxes, string label, bool ticked)
    {
        boxes.ConstantItem(26).Border(Line).BorderColor(Ink).AlignCenter().AlignMiddle().PaddingVertical(2).Text(label).FontSize(7.8f).AlignCenter();
        boxes.ConstantItem(17).Border(Line).BorderColor(Ink).AlignCenter().AlignMiddle().Padding(2).Element(box =>
        {
            if (ticked) box.Svg(TickSvg);
        });
    }

    private static byte[] QrPng(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(12);
    }

    /// <summary>The signature strokes (normalised x/y numbers) drawn into a box of the given size; blank when they cannot be read.</summary>
    private static string SignatureSvg(string strokesJson, double width, double height)
    {
        var empty = string.Create(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\"/>");
        try
        {
            using var document = JsonDocument.Parse(strokesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return empty;
            var svg = new StringBuilder();
            svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
            foreach (var stroke in document.RootElement.EnumerateArray())
            {
                var points = stroke.ValueKind == JsonValueKind.Object && stroke.TryGetProperty("p", out var encoded) ? encoded : stroke;
                if (points.ValueKind != JsonValueKind.Array) continue;
                var numbers = points.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.Number).Select(value => value.GetDouble()).ToArray();
                if (numbers.Length < 4) continue;
                svg.Append("<polyline fill=\"none\" stroke=\"#1B2433\" stroke-width=\"1.3\" stroke-linecap=\"round\" stroke-linejoin=\"round\" points=\"");
                for (var index = 0; index + 1 < numbers.Length; index += 2)
                    svg.Append(CultureInfo.InvariantCulture, $"{numbers[index] * width:0.##},{numbers[index + 1] * height:0.##} ");
                svg.Append("\"/>");
            }

            svg.Append("</svg>");
            return svg.ToString();
        }
        catch (JsonException)
        {
            return empty;
        }
    }

    private static string Format(DateTimeOffset value) => value.UtcDateTime.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Format(DateOnly value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string SafeFileName(string value)
    {
        var cleaned = new string(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-').ToArray());
        return cleaned.Trim('-') is { Length: > 0 } name ? name : "certificate";
    }
}
