using System.Globalization;
using Inspecta.Application.Pdf.Models;
using Inspecta.Infrastructure.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Inspecta.Infrastructure.Pdf.Templates;

/// <summary>
/// The Report of Thorough Examination of one machine, laid out like the company's Word forms. One layout serves every
/// machine: the technical fields and the checklist come from the machine's template, the rest (client, dates,
/// questionnaire, notes, signatures and QR code) is the same for all. The QR is printed from the inspector's first submit,
/// so the technical manager previews it too; the DRAFT mark stays until the certificate is approved.
/// </summary>
public sealed class CertificatePdfTemplate : QuestPdfTemplate<CertificatePdfModel>
{
    private static readonly Color Ink = Colors.Black;
    private static readonly Color TitleBlue = Color.FromHex("#2B66A6");
    private static readonly Color LinkBlue = Color.FromHex("#1F5C8E");
    private static readonly Color Green = Color.FromHex("#00B050");
    private static readonly Color FailedFill = Color.FromHex("#FBE3E1");

    private const float Line = 0.7f;
    private const float Pad = 2.4f;
    private const float Gap = 4f;
    // The built-in identity: used by a team that has no company profile.
    private const string BuiltInSigner = "Ahmed Khalil";
    private const string BuiltInPhones = "+201287374933 - 01035945426";
    private const string BuiltInTitle = "Integrity International Inspection";
    private const string BuiltInIssuer = "Integrity for inspection and Engineering Solutions";
    private static readonly QrCodeRenderer QrCodes = new();

    private static string[] GeneralNotes(CertificatePdfModel model) =>
    [
        "This Certificate becomes invalid if any Repair/modification is done that affects strength or stability.",
        "Any holder of this document is advised that the information contained hereon is limited to the accessible portions of the item examined only.",
        "Equipment has been correctly installed and fit for the purpose",
        $"This Certificate should be signed, stamped and carries unique {Details(model)?.IssuerName ?? BuiltInIssuer} certificate number"
    ];

    // The company details of the profile; none when the team has no profile or keeps the built-in details.
    private static CompanyBranding? Details(CertificatePdfModel model) => model.Company is { UsesBuiltInDetails: false } ? model.Company : null;

    private static string HeaderTitle(CertificatePdfModel model) => Details(model)?.CertificateTitle ?? BuiltInTitle;
    // A company that has not set its own logo or accreditation marks gets the built-in ones.
    private static byte[]? Logo(CertificatePdfModel model) => model.Company?.LogoPng ?? PdfAssets.CertificateLogo;
    private static byte[]? Accreditations(CertificatePdfModel model) => model.Company?.AccreditationsPng ?? PdfAssets.Accreditations;
    private static Color Blue(CertificatePdfModel model) => model.Company is null ? BandBlue : Color.FromHex(model.Company.PrimaryColor);
    private static Color Accent(CertificatePdfModel model) => model.Company is null ? BandGreen : Color.FromHex(model.Company.AccentColor);

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
            // The frame, the faded logo and the bands of the company colours on every page.
            page.Background().Element(container => ComposeBackground(container, model));
            page.Header().PaddingHorizontal(10).PaddingTop(8).Element(container => ComposeHeader(container, model));
            page.Content().PaddingHorizontal(10).PaddingVertical(4).Element(container => ComposeBody(container, model));
            page.Footer().PaddingHorizontal(10).PaddingBottom(6).Element(container => ComposeFooter(container, model));
            if (model.IsDraft)
                page.Foreground().AlignCenter().AlignMiddle().Rotate(-35)
                    .Text("DRAFT").FontSize(110).Bold().FontColor(Color.FromRGB(190, 197, 210).WithAlpha(90));
        });

    // ---- Page background (on every page) ------------------------------------------------------------------------

    private static readonly Color BandBlue = Color.FromRGB(24, 72, 150);
    private static readonly Color BandGreen = Color.FromRGB(52, 160, 70);
    private const float BandHeight = 14;

    // One logo in the middle, faded by a nearly opaque white layer over it (the image is stored once in the file), and a band
    // at the top and the bottom of the page. All of it is behind the text, so nothing printed on the certificate is touched.
    private static void ComposeBackground(IContainer container, CertificatePdfModel model) =>
        container.Layers(layers =>
        {
            if (Logo(model) is { Length: > 0 } logo)
            {
                layers.Layer().AlignCenter().AlignMiddle().Width(360).Image(logo).FitWidth();
                layers.Layer().Background(Colors.White.WithAlpha(232));
            }

            var blue = Blue(model);
            var accent = Accent(model);
            layers.Layer().Unconstrained().Width(PageSizes.A4.Width).Height(BandHeight).Row(row =>
            {
                row.RelativeItem(72).Background(blue);
                row.RelativeItem(28).Background(accent);
            });
            layers.Layer().Unconstrained().TranslateY(PageSizes.A4.Height - BandHeight).Width(PageSizes.A4.Width).Height(BandHeight).Row(row =>
            {
                row.RelativeItem(28).Background(accent);
                row.RelativeItem(72).Background(blue);
            });

            layers.PrimaryLayer().Border(1).BorderColor(Ink);
        });

    // ---- Header and footer (on every page) ----------------------------------------------------------------------

    protected override void ComposeHeader(IContainer container, CertificatePdfModel model) =>
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(lines =>
                {
                    var company = Details(model);
                    lines.Item().Text(HeaderTitle(model)).Bold();
                    var tagline = company is null ? "Industrial & Energy Services." : company.Tagline;
                    if (!string.IsNullOrWhiteSpace(tagline)) lines.Item().Text(tagline);
                    lines.Item().Text(company is null ? "21 Nakib Sharif Ramzy st, Smouha, Alex., Egypt." : company.Address);
                    var phones = company is null ? BuiltInPhones : string.Join(" - ", company.Phones);
                    if (phones.Length > 0)
                        lines.Item().Text(text =>
                        {
                            text.Span("Mobile No: ").Bold();
                            text.Span(phones).Bold().FontColor(Green);
                        });
                    // The technical manager's email, then the coordinator's of the job.
                    var contacts = model.ContactEmails();
                    foreach (var email in contacts.Count > 0
                                 ? contacts
                                 : company is null ? ["Ibrahim.ashraf@integrity-intl.com", "Hager.samy@integrity-intl.com"] : company.Emails.Take(2).ToArray())
                        lines.Item().Element(content => Email(content, email));
                });
                if (Logo(model) is { Length: > 0 } headerLogo)
                    row.ConstantItem(70).AlignRight().Height(62).Image(headerLogo).FitArea();
            });
            column.Item().AlignCenter().Text("REPORT OF THOROUGH EXAMINATION").Bold().FontSize(13.5f).FontColor(TitleBlue);
            column.Item().PaddingTop(2).Text(text =>
            {
                text.Span("Certificate No.: ").Bold();
                text.Span(model.CertificateNumber ?? "(on approval)").Bold();
            });
            column.Item().PaddingBottom(3).Text(text =>
            {
                text.Span("Certificate Date: ").Bold();
                text.Span(model.CertificateDate is { } issued ? Format(issued) : "(on approval)").Bold();
            });
        });

    private static void Email(IContainer container, string address) =>
        container.Text(text =>
        {
            text.Span("Email: ").Bold();
            text.Span(address).Bold().Underline().FontColor(LinkBlue);
        });

    protected override void ComposeFooter(IContainer container, CertificatePdfModel model) =>
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignBottom().Column(lines =>
                {
                    var company = Details(model);
                    lines.Item().Text($"{HeaderTitle(model)}. All rights reserved.").Bold().FontSize(6.8f);
                    lines.Item().Text($"{HeaderTitle(model)} | {(company is null ? "21 Nakib Sharif Ramzy St., Smouha, Alexandria, Egypt" : company.Address)}").Bold().FontSize(6.8f);
                    var tel = company is null ? "+201287374933 / 01035945426" : string.Join(" / ", company.Phones);
                    if (tel.Length > 0) lines.Item().Text($"Tel: {tel}").Bold().FontSize(6.8f).FontColor(Green);
                    var emails = company is null ? "Ibrahim.ashraf@integrity-intl.com / Info@integrity-intl.com" : string.Join(" / ", company.Emails);
                    if (emails.Length > 0) lines.Item().Text($"Email: {emails}").Bold().FontSize(6.8f).FontColor(Color.FromHex("#1565C0"));
                    lines.Item().Text("This Certificate is confidential and intended solely for the use of the client for whom it was prepared.").Bold().FontSize(6.8f);
                    lines.Item().Text("Unauthorized use, reproduction, or distribution is strictly prohibited.").Bold().FontSize(6.8f);
                });
                if (Accreditations(model) is { Length: > 0 } accreditations)
                    row.ConstantItem(150).AlignRight().AlignBottom().Height(46).Image(accreditations).FitArea();
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
            column.Item().Element(content => ComposeClient(content, model));
            column.Item().PaddingTop(Gap).Element(content => ComposeDates(content, model));
            column.Item().PaddingTop(Gap).Element(content => ComposeItem(content, model));
            column.Item().PaddingTop(Gap).Element(content => ComposeFields(content, model.Fields));
            column.Item().PaddingTop(Gap).Element(content => ComposeQuestionnaire(content, model.Questionnaire));
            // The notes and the signatures stay on one page.
            column.Item().ShowEntire().Column(closing =>
            {
                closing.Item().Element(content => ComposeGeneralNotes(content, model));
                closing.Item().Element(content => ComposeSignatures(content, model));
            });
            if (model.Checklist.Count > 0)
            {
                column.Item().PageBreak();
                column.Item().Element(content => ComposeChecklist(content, model.Checklist));
            }
        });

    private static void ComposeClient(IContainer container, CertificatePdfModel model) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                // The same grid as the tables below: a label column of a quarter, so every vertical line of the page lines up.
                columns.RelativeColumn();
                columns.RelativeColumn(3);
            });
            foreach (var (label, value) in new[]
                     {
                         ("Client Name:", model.ClientName),
                         ("Client Address:", model.ClientAddress),
                         ("Client Representative:", model.ClientRepresentative)
                     })
            {
                table.Cell().Element(Cell).Text(label).Bold().FontSize(9.2f);
                table.Cell().Element(Cell).AlignMiddle().Text(Show(value)).FontSize(9f);
            }
        });

    private static void ComposeDates(IContainer container, CertificatePdfModel model) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });
            var lastCertificate = string.Join(" / ",
                new[] { model.LastCertificateNumber, model.LastCertificateBy }.Where(value => !string.IsNullOrWhiteSpace(value)));
            Label(table, "Last thorough examination date:");
            Value(table, model.LastExaminationDate is { } last ? Format(last) : "N/A");
            Label(table, "Last Certificate No./Made by:");
            Value(table, lastCertificate.Length > 0 ? lastCertificate : "N/A");
            Label(table, "Through examination date:");
            Value(table, model.ExaminationDate is { } exam ? Format(exam) : null, bold: true);
            Label(table, "Next examination date:");
            Value(table, model.NextExaminationDate is { } next ? Format(next) : null, bold: true);
        });

    private static void ComposeItem(IContainer container, CertificatePdfModel model) =>
        container.Table(table =>
        {
            FourColumns(table);
            Head(table, "Inspected Item:");
            Head(table, "Inspection Standard:");
            Head(table, "Test Type:");
            Head(table, "Conclusion/Remarks:");
            Value(table, model.ItemName);
            table.Cell().Element(Cell).AlignCenter().AlignMiddle().Column(lines =>
            {
                if (model.Standards.Count == 0) lines.Item().AlignCenter().Text("—");
                foreach (var standard in model.Standards) lines.Item().AlignCenter().Text(standard);
            });
            Value(table, model.TestType);
            // One word only: Satisfactory or Unsatisfactory.
            Value(table, model.Conclusion, bold: true);
        });

    /// <summary>The template's technical fields, four to a row: the labels on one line and the values under them.</summary>
    private static void ComposeFields(IContainer container, IReadOnlyList<CertificatePdfField> fields) =>
        container.Table(table =>
        {
            FourColumns(table);
            for (var start = 0; start < fields.Count; start += 4)
            {
                var chunk = fields.Skip(start).Take(4).ToArray();
                // Short rows are completed with empty cells so the grid keeps its borders.
                foreach (var field in chunk) Head(table, field.Label.TrimEnd(':') + ":");
                for (var filler = chunk.Length; filler < 4; filler++) table.Cell().Element(Cell);
                foreach (var field in chunk) Value(table, field.Value);
                for (var filler = chunk.Length; filler < 4; filler++) table.Cell().Element(Cell);
            }
        });

    private static void ComposeQuestionnaire(IContainer container, CertificatePdfQuestionnaire q) =>
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(Cell).Column(left =>
                {
                    left.Item().Element(content => YesNo(content, "Is this the first examination after installation or assembly at a new site or location?", q.FirstExamination));
                    left.Item().PaddingTop(4).Element(content => YesNo(content, "If the answer to the above question is YES has the equipment been installed correctly?",
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
            column.Item().Element(Cell).Text($"Identification of any part found to have a defect which is or could become a danger to people and a description of the defect: {q.DefectDescription}");
            column.Item().Element(Cell).Element(content => YesNo(content, "Is the above an existing or imminent danger to persons *Note-This is a reportable defect", q.ExistingDanger));
            column.Item().Row(row =>
            {
                row.RelativeItem(3).Element(Cell).Text("Is the above a defect which is not yet but could become a danger to people:(If YES state the date by when)");
                row.RelativeItem(1).Element(Cell).Text($"YES by:  {(q.FutureDanger == true && q.FutureDangerBy is { } by ? Format(by) : "N/A")}");
            });
            column.Item().Element(Cell).Text($"Particulars of any repair, renewal or alteration required to remedy the defect identified above: {q.RepairsRequired}");
            column.Item().Element(Cell).Text($"Particulars of any tests carried out as part of the examination: {q.TestsCarriedOut}");
            column.Item().Element(Cell).MinHeight(16).Text($"Note: {q.Note}");
        });

    private static void ComposeGeneralNotes(IContainer container, CertificatePdfModel model) =>
        container.Row(row =>
        {
            row.RelativeItem(1).Element(Cell).AlignMiddle().AlignCenter().Text("General Notes and Recommendations").AlignCenter();
            row.RelativeItem(3).Element(Cell).Column(column =>
            {
                foreach (var note in GeneralNotes(model))
                    column.Item().Row(line =>
                    {
                        line.ConstantItem(9).Text("•");
                        line.RelativeItem().Text(note).FontSize(7.3f);
                    });
            });
        });

    private static void ComposeSignatures(IContainer container, CertificatePdfModel model) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });
            Head(table, "Name & Qualifications of person making this report");
            Head(table, "Name of person signing or authenticating this report on behalf of the author");
            Head(table, $"For {Details(model)?.IssuerName ?? BuiltInIssuer}");
            Head(table, "QR Code");

            table.Cell().Element(Cell).Height(54).AlignMiddle().AlignCenter().Column(column =>
            {
                column.Item().AlignCenter().Text(model.PreparedByName ?? "—").Bold();
                if (!string.IsNullOrWhiteSpace(model.PreparedByQualifications))
                    column.Item().AlignCenter().Text(model.PreparedByQualifications).FontSize(6.2f);
            });
            // The technical manager is named in the middle of the box; the hand-drawn signature is not printed.
            table.Cell().Element(Cell).Height(54).AlignMiddle().AlignCenter().Text(model.SignedBy ?? "—").Bold();
            table.Cell().Element(Cell).Height(54).AlignMiddle().AlignCenter().Text(Details(model) is { } details ? details.SignerName : BuiltInSigner).Bold();
            // Filled from the first submit; once approved, scanning it opens the public check page with the PDF.
            table.Cell().Element(Cell).Height(54).AlignMiddle().AlignCenter().Element(qr =>
            {
                if (string.IsNullOrWhiteSpace(model.QrData)) return;
                qr.Width(48).Height(48).Image(QrCodes.RenderPng(model.QrData)).FitArea();
            });
        });

    // ---- Checklist ----------------------------------------------------------------------------------------------

    private sealed record ChecklistLine(string Label, string? Value, string? Note, bool IsSection);

    /// <summary>
    /// The checklist as in the forms: one grid with two columns of sections. The sections keep their order and are
    /// split where the two columns come out closest in length; the columns are filled line by line.
    /// </summary>
    private static void ComposeChecklist(IContainer container, IReadOnlyList<CertificatePdfSection> sections)
    {
        var lengths = sections.Select(section => section.Rows.Count + 1).ToArray();
        var total = lengths.Sum();
        var split = sections.Count;
        var best = int.MaxValue;
        for (int index = 1, left = 0; index <= sections.Count; index++)
        {
            left += lengths[index - 1];
            var longest = Math.Max(left, total - left);
            if (longest < best) (best, split) = (longest, index);
        }

        var leftLines = Lines(sections.Take(split), "Inspected");
        var rightLines = Lines(sections.Skip(split), "INSP");
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.ConstantColumn(52);
                columns.RelativeColumn();
                columns.ConstantColumn(52);
            });
            for (var index = 0; index < Math.Max(leftLines.Count, rightLines.Count); index++)
            {
                ChecklistCells(table, index < leftLines.Count ? leftLines[index] : null);
                ChecklistCells(table, index < rightLines.Count ? rightLines[index] : null);
            }
        });
    }

    // A section title line, then its rows. The first title of a column carries the column's heading.
    private static List<ChecklistLine> Lines(IEnumerable<CertificatePdfSection> sections, string heading)
    {
        var lines = new List<ChecklistLine>();
        foreach (var section in sections)
        {
            lines.Add(new ChecklistLine(section.Title, lines.Count == 0 ? heading : null, null, IsSection: true));
            lines.AddRange(section.Rows.Select(row => new ChecklistLine(row.Label, row.Value, row.Note, IsSection: false)));
        }

        return lines;
    }

    private static void ChecklistCells(TableDescriptor table, ChecklistLine? line)
    {
        if (line is null)
        {
            table.Cell().Element(Cell);
            table.Cell().Element(Cell);
            return;
        }

        if (line.IsSection)
        {
            table.Cell().Element(Cell).Text(line.Label).Bold().Underline().FontSize(8.2f);
            table.Cell().Element(Cell).AlignCenter().Text(line.Value ?? string.Empty).FontSize(8.2f);
            return;
        }

        table.Cell().Element(Cell).Text(text =>
        {
            text.Span(line.Label).FontSize(7.8f);
            if (!string.IsNullOrWhiteSpace(line.Note))
                text.Span($"  ({line.Note})").FontSize(7).Italic().FontColor(PdfTheme.Muted);
        });
        var failed = line.Value == "No";
        table.Cell().Element(c => TintedCell(c, failed ? FailedFill : Colors.White)).AlignCenter()
            .Text(line.Value ?? "—").FontSize(7.8f);
    }

    // ---- Small building blocks ----------------------------------------------------------------------------------

    private static IContainer Cell(IContainer container) =>
        container.Border(Line).BorderColor(Ink).Padding(Pad);

    // The fill goes behind the padding too, so the whole cell is coloured.
    private static IContainer TintedCell(IContainer container, Color fill) =>
        container.Border(Line).BorderColor(Ink).Background(fill).Padding(Pad);

    private static void FourColumns(TableDescriptor table) =>
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn();
            columns.RelativeColumn();
            columns.RelativeColumn();
            columns.RelativeColumn();
        });

    private static void Head(TableDescriptor table, string label) =>
        table.Cell().Element(Cell).AlignCenter().AlignMiddle().Text(label).Bold().AlignCenter();

    private static void Label(TableDescriptor table, string label) =>
        table.Cell().Element(Cell).AlignMiddle().Text(label).Bold();

    private static void Value(TableDescriptor table, string? value, bool bold = false)
    {
        var text = table.Cell().Element(Cell).AlignCenter().AlignMiddle().Text(Show(value)).AlignCenter();
        if (bold) text.Bold();
    }

    private static string Show(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

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

    private static string Format(DateTimeOffset value) => value.UtcDateTime.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Format(DateOnly value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string SafeFileName(string value)
    {
        var cleaned = new string(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-').ToArray());
        return cleaned.Trim('-') is { Length: > 0 } name ? name : "certificate";
    }
}
