using System.Globalization;
using Inspecta.Application.Pdf.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Inspecta.Infrastructure.Pdf.Templates;

/// <summary>
/// The Report of Thorough Examination of one machine. One layout serves every machine: the technical fields and the
/// checklist come from the machine's template, the rest (questionnaire, notes, signatures) is the same for all.
/// </summary>
public sealed class CertificatePdfTemplate : QuestPdfTemplate<CertificatePdfModel>
{
    private static readonly string[] CompanyLines =
    [
        "Integrity International Inspection",
        "Industrial & Energy Services.",
        "21 Nakib Sharif Ramzy st, Smouha, Alex., Egypt.",
        "Mobile No: +201287374933 - 01035945426",
        "Email: Ibrahim.ashraf@integrity-intl.com",
        "Email: Hager.samy@integrity-intl.com"
    ];

    private static readonly string[] GeneralNotes =
    [
        "This Certificate becomes invalid if any Repair/modification is done that affects strength or stability.",
        "Any holder of this document is advised that the information contained hereon is limited to the accessible portions of the item examined only.",
        "Equipment has been correctly installed and fit for the purpose.",
        "This Certificate should be signed, stamped and carries unique Integrity certificate number."
    ];

    private static readonly string[] FooterLines =
    [
        "Integrity International Inspection. All rights reserved.",
        "Integrity International Inspection | 21 Nakib Sharif Ramzy St., Smouha, Alexandria, Egypt | Tel: +201287374933 / 01035945426",
        "Email: Ibrahim.ashraf@integrity-intl.com / Info@integrity-intl.com",
        "This Certificate is confidential and intended solely for the use of the client for whom it was prepared. Unauthorized use, reproduction, or distribution is strictly prohibited."
    ];

    private const string CompanySigner = "Ahmed Khalil";
    private const float CellPadding = 3;

    public override string FileName(CertificatePdfModel model) =>
        $"certificate-{SafeFileName(model.CertificateNumber ?? model.ItemName)}.pdf";

    protected override string Title(CertificatePdfModel model) =>
        $"Report of Thorough Examination {model.CertificateNumber ?? "(draft)"} - {model.ItemName}";

    protected override void ComposeDocument(IDocumentContainer document, CertificatePdfModel model) =>
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(style => style.FontFamily(PdfTheme.FontFamily).FontSize(8.5f).FontColor(PdfTheme.Body));
            page.Header().Element(container => ComposeHeader(container, model));
            page.Content().PaddingVertical(6).Element(container => ComposeBody(container, model));
            page.Footer().Element(container => ComposeFooter(container, model));
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
                    lines.Item().Text(CompanyLines[0]).Bold().FontSize(11).FontColor(PdfTheme.Navy);
                    foreach (var line in CompanyLines.Skip(1))
                        lines.Item().Text(line).FontSize(7.5f).FontColor(PdfTheme.Muted);
                });
                row.ConstantItem(70).AlignRight().Height(40).Image(PdfAssets.Logo).FitArea();
            });
            column.Item().PaddingTop(4).LineHorizontal(1.2f).LineColor(PdfTheme.Navy);
            column.Item().PaddingTop(4).AlignCenter().Text("REPORT OF THOROUGH EXAMINATION").Bold().FontSize(13).FontColor(PdfTheme.Navy);
            column.Item().PaddingTop(2).Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span("Certificate No.: ").Bold();
                    text.Span(model.CertificateNumber ?? "— (issued on approval)");
                });
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Certificate Date: ").Bold();
                    text.Span(model.CertificateDate is { } date ? Format(date) : "—");
                });
            });
        });

    protected override void ComposeFooter(IContainer container, CertificatePdfModel model) =>
        container.Column(column =>
        {
            column.Item().LineHorizontal(0.6f).LineColor(PdfTheme.Line);
            foreach (var line in FooterLines)
                column.Item().AlignCenter().Text(line).FontSize(6.3f).FontColor(PdfTheme.Muted);
            column.Item().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(7).FontColor(PdfTheme.Muted));
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
            column.Spacing(6);
            column.Item().Element(content => ComposeClient(content, model));
            column.Item().Element(content => ComposeDates(content, model));
            column.Item().Element(content => ComposeItem(content, model));
            column.Item().Element(content => ComposeFields(content, model));
            column.Item().Element(content => ComposeQuestionnaire(content, model.Questionnaire));
            column.Item().Element(ComposeGeneralNotes);
            column.Item().Element(content => ComposeSignatures(content, model));
            if (model.Checklist.Count > 0)
            {
                column.Item().PageBreak();
                column.Item().Text($"Inspection checklist - {model.ItemName}").Bold().FontSize(11).FontColor(PdfTheme.Navy);
                foreach (var section in model.Checklist)
                    column.Item().Element(content => ComposeChecklistSection(content, section));
            }
        });

    private static void ComposeClient(IContainer container, CertificatePdfModel model) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns => { columns.ConstantColumn(130); columns.RelativeColumn(); });
            Row(table, "Client Name:", model.ClientName);
            Row(table, "Client Address:", model.ClientAddress);
            Row(table, "Client Representative:", model.ClientRepresentative);
        });

    private static void ComposeDates(IContainer container, CertificatePdfModel model) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(130);
                columns.RelativeColumn();
                columns.ConstantColumn(130);
                columns.RelativeColumn();
            });
            LabelCell(table, "Last thorough examination date:");
            ValueCell(table, model.LastExaminationDate is { } last ? Format(last) : null);
            LabelCell(table, "Last Certificate No./Made by:");
            ValueCell(table, null);
            LabelCell(table, "Thorough examination date:");
            ValueCell(table, model.ExaminationDate is { } exam ? Format(exam) : null);
            LabelCell(table, "Next examination date:");
            ValueCell(table, model.NextExaminationDate is { } next ? Format(next) : null);
        });

    private static void ComposeItem(IContainer container, CertificatePdfModel model) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.RelativeColumn(3);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
            });
            LabelCell(table, "Inspected Item:");
            LabelCell(table, "Inspection Standard:");
            LabelCell(table, "Test Type:");
            LabelCell(table, "Conclusion/Remarks:");
            ValueCell(table, model.ItemName, bold: true);
            table.Cell().Element(Cell).Column(column =>
            {
                if (model.Standards.Count == 0) column.Item().Text("—");
                foreach (var standard in model.Standards) column.Item().Text(standard);
            });
            ValueCell(table, model.TestType);
            ValueCell(table, model.Conclusion, bold: true);
        });

    private static void ComposeFields(IContainer container, CertificatePdfModel model) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                for (var index = 0; index < 4; index++) columns.RelativeColumn();
            });
            foreach (var field in model.Fields)
                table.Cell().Element(Cell).Column(column =>
                {
                    column.Item().Text(field.Label).Bold().FontSize(7.3f).FontColor(PdfTheme.Muted);
                    column.Item().Text(string.IsNullOrWhiteSpace(field.Value) ? "—" : field.Value);
                });
            // Complete the last row so the grid keeps its border.
            for (var filler = (4 - model.Fields.Count % 4) % 4; filler > 0; filler--)
                table.Cell().Element(Cell).Text(string.Empty);
        });

    private static void ComposeQuestionnaire(IContainer container, CertificatePdfQuestionnaire q) =>
        container.Column(column =>
        {
            column.Spacing(0);
            column.Item().Element(content => YesNoTable(content, [
                ("Is this the first examination after installation or assembly at a new site or location?", q.FirstExamination),
                .. (q.FirstExamination == true
                    ? new (string, bool?)[] { ("If so, has the equipment been installed correctly?", q.InstalledCorrectly) }
                    : []),
                ("Was the examination carried out within an interval of 6 months?", q.Within6Months),
                ("Was the examination carried out within an interval of 12 months?", q.Within12Months),
                ("Was the examination carried out in accordance with an examination scheme?", q.ExaminationScheme),
                ("Was the examination carried out after the occurrence of exceptional circumstances?", q.ExceptionalCircumstances)
            ]));
            column.Item().Element(content => TextBlock(content,
                "Identification of any part found to have a defect which is or could become a danger to people and a description of the defect:",
                q.DefectDescription));
            column.Item().Element(content => YesNoTable(content, [
                ("Is the above an existing or imminent danger to persons? *Note - this is a reportable defect", q.ExistingDanger),
                ("Is the above a defect which is not yet but could become a danger to people?" +
                 (q.FutureDanger == true && q.FutureDangerBy is { } by ? $" (YES by: {Format(by)})" : " (If YES state the date by when)"), q.FutureDanger)
            ]));
            column.Item().Element(content => TextBlock(content,
                "Particulars of any repair, renewal or alteration required to remedy the defect identified above:", q.RepairsRequired));
            column.Item().Element(content => TextBlock(content,
                "Particulars of any tests carried out as part of the examination:", q.TestsCarriedOut));
            column.Item().Element(content => TextBlock(content, "Note:", q.Note));
        });

    private static void ComposeGeneralNotes(IContainer container) =>
        container.Element(Cell).Column(column =>
        {
            column.Item().Text("General Notes and Recommendations").Bold();
            foreach (var note in GeneralNotes)
                column.Item().Text(note).FontSize(7.8f);
        });

    private static void ComposeSignatures(IContainer container, CertificatePdfModel model) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.RelativeColumn(3);
                columns.RelativeColumn(3);
                columns.RelativeColumn(2);
            });
            LabelCell(table, "Name & Qualifications of person making this report");
            LabelCell(table, "Name of person signing or authenticating this report on behalf of the author");
            LabelCell(table, "For Integrity for inspection and Engineering Solutions");
            LabelCell(table, "QR Code");
            ValueCell(table, model.PreparedBy);
            ValueCell(table, model.SignedBy);
            ValueCell(table, CompanySigner);
            table.Cell().Element(Cell).Column(column =>
            {
                column.Item().Height(52).AlignMiddle().AlignCenter()
                    .Text(string.IsNullOrWhiteSpace(model.QrData) ? string.Empty : "QR").FontColor(PdfTheme.Muted);
                if (!string.IsNullOrWhiteSpace(model.VerificationCode))
                    column.Item().AlignCenter().Text($"Code: {model.VerificationCode}").FontSize(7);
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
            table.Cell().ColumnSpan(4).Element(c => c.Border(0.5f).BorderColor(PdfTheme.Line).Background(PdfTheme.Band).Padding(CellPadding))
                .Text(section.Title).Bold().FontSize(8.5f).FontColor(PdfTheme.Navy);

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
        table.Cell().Element(c => Cell(c).Background(failed ? Color.FromHex("#FBE3E1") : PdfTheme.White).AlignCenter())
            .Text(row.Value ?? "—").FontSize(7.8f).Bold();
    }

    // ---- Small building blocks ----------------------------------------------------------------------------------

    private static IContainer Cell(IContainer container) =>
        container.Border(0.5f).BorderColor(PdfTheme.Line).Padding(CellPadding);

    private static void LabelCell(TableDescriptor table, string label) =>
        table.Cell().Element(c => Cell(c).Background(PdfTheme.Band)).Text(label).Bold().FontSize(7.8f);

    private static void ValueCell(TableDescriptor table, string? value, bool bold = false)
    {
        var text = table.Cell().Element(Cell).Text(string.IsNullOrWhiteSpace(value) ? "—" : value);
        if (bold) text.Bold();
    }

    private static void Row(TableDescriptor table, string label, string? value)
    {
        LabelCell(table, label);
        ValueCell(table, value);
    }

    private static void TextBlock(IContainer container, string label, string? value) =>
        container.Element(Cell).Column(column =>
        {
            column.Item().Text(label).Bold().FontSize(7.8f);
            column.Item().Text(string.IsNullOrWhiteSpace(value) ? "—" : value);
        });

    private static void YesNoTable(IContainer container, IReadOnlyList<(string Question, bool? Answer)> rows) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(38);
                columns.ConstantColumn(38);
            });
            foreach (var (question, answer) in rows)
            {
                table.Cell().Element(Cell).Text(question);
                AnswerCell(table, "YES", answer == true);
                AnswerCell(table, "NO", answer == false);
            }
        });

    private static void AnswerCell(TableDescriptor table, string label, bool selected) =>
        table.Cell().Element(c => Cell(c).Background(selected ? PdfTheme.Green : PdfTheme.White).AlignCenter())
            .Text(label).Bold().FontSize(7.8f).FontColor(selected ? PdfTheme.White : PdfTheme.Muted);

    private static string Format(DateTimeOffset value) => value.UtcDateTime.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    private static string Format(DateOnly value) => value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    private static string SafeFileName(string value)
    {
        var cleaned = new string(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-').ToArray());
        return cleaned.Trim('-') is { Length: > 0 } name ? name : "certificate";
    }
}
