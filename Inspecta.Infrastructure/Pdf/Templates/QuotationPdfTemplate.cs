using System.Globalization;
using Inspecta.Application.Pdf;
using Inspecta.Application.Pdf.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Inspecta.Infrastructure.Pdf.Templates;

/// <summary>
/// Technical and commercial offer, laid out like the reference quotation: illustrated cover, table of contents,
/// cover letter, company pages, scope of work, budget with notes, terms and conditions, and a thank-you page.
/// </summary>
public sealed class QuotationPdfTemplate : QuestPdfTemplate<QuotationPdfModel>
{
    private const float PageWidth = 595f;
    private const float PageHeight = 842f;

    private static readonly string[] ContentsTitles =
    [
        "Cover Letter", "About Us", "Our Vision", "Integrity Services", "Scope of Work", "Project Budget", "Terms and Conditions"
    ];

    public override string FileName(QuotationPdfModel model) =>
        $"quotation-{SafeFileName(model.ProposalNumber)}.pdf";

    protected override string Title(QuotationPdfModel model) =>
        $"Technical and commercial offer {model.ProposalNumber}";

    protected override void ComposeDocument(IDocumentContainer document, QuotationPdfModel model)
    {
        var company = model.Company ?? DefaultCompanyProfile.Instance;

        ComposeCover(document, model, company);
        StandardPage(document, model, company, content => ComposeContents(content));
        StandardPage(document, model, company, content => ComposeCoverLetter(content, model, company));
        StandardPage(document, model, company, content => ComposeAboutAndVision(content, company));
        foreach (var section in company.Services)
            StandardPage(document, model, company, content => ComposeServices(content, section));
        StandardPage(document, model, company, content => ComposeScope(content, model));
        StandardPage(document, model, company, content => ComposeBudget(content, model, company));
        StandardPage(document, model, company, content => ComposeTerms(content, model, company));
        ComposeThankYou(document, company);
    }

    // ---- Cover -------------------------------------------------------------------------------------------------

    // The illustration, title, labels and signature line are part of the cover image. The sample values printed in
    // it are covered with white boxes and the real values are written over them.
    private static void ComposeCover(IDocumentContainer document, QuotationPdfModel model, QuotationCompanyProfile company) =>
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.DefaultTextStyle(style => style.FontFamily(PdfTheme.FontFamily).FontColor(PdfTheme.Navy).Bold());
            page.Content().Layers(layers =>
            {
                layers.PrimaryLayer().Image(PdfAssets.QuotationCover).FitUnproportionally();

                CoverField(layers, 168.4f, 453.8f, 320f, 43f, model.ClientName, 26, 16);
                CoverField(layers, 138.1f, 533.9f, 156.6f, 26.1f, model.ContactName ?? "-", 20, 11);
                CoverField(layers, 346.1f, 533.9f, 152f, 26.1f, model.Subject, 14, 8, maxLines: 2);
                CoverField(layers, 108.6f, 600f, 149f, 26f, model.QuotationNumber, 20, 10);
                CoverField(layers, 276.2f, 600f, 94.3f, 26f, FormatDate(model.IssuedAt), 16.5f, 9);
                CoverField(layers, 388.1f, 600f, 96f, 26f, FormatDate(model.ValidUntil), 16.5f, 9, PdfTheme.DarkGreen);
                CoverField(layers, 79.1f, 726.6f, 215.6f, 25.3f, model.PreparedBy, 18, 11);
                CoverField(layers, 79.1f, 752.4f, 215.6f, 14f, model.PreparedByTitle, 10.5f, 8, PdfTheme.Muted, bold: false);
            });
            _ = company;
        });

    private static void CoverField(
        LayersDescriptor layers,
        float x,
        float y,
        float width,
        float height,
        string value,
        float maxSize,
        float minSize,
        Color? color = null,
        bool bold = true,
        int maxLines = 1)
    {
        var size = FitFontSize(value, width - 4, maxSize, minSize, bold ? 0.6f : 0.52f);
        var block = layers.Layer()
            .OffsetX(x).OffsetY(y)
            .Width(width).Height(height)
            .Background(PdfTheme.White)
            .PaddingLeft(2)
            .AlignMiddle()
            .Text(value)
            .FontSize(size)
            .FontColor(color ?? PdfTheme.Navy)
            .LineHeight(1f)
            .ClampLines(maxLines);
        if (bold) block.Bold();
        else block.NormalWeight();
    }

    // ---- Standard page frame ---------------------------------------------------------------------------------------

    private static void StandardPage(
        IDocumentContainer document,
        QuotationPdfModel model,
        QuotationCompanyProfile company,
        Action<IContainer> content) =>
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginTop(28);
            page.MarginBottom(22);
            page.MarginHorizontal(PdfTheme.PageMargin);
            page.PageColor(PdfTheme.White);
            page.DefaultTextStyle(style => style
                .FontFamily(PdfTheme.FontFamily)
                .FontSize(10)
                .FontColor(PdfTheme.Body)
                .LineHeight(1.3f));
            page.Background().Svg(PdfAssets.GridSvg(PageWidth, PageHeight));
            page.Header().Element(header => ComposePageHeader(header, model));
            page.Content().PaddingTop(16).Element(content);
            page.Footer().Element(footer => ComposePageFooter(footer, company));
        });

    private static void ComposePageHeader(IContainer container, QuotationPdfModel model) =>
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(70).Width(54).Height(50).Image(PdfAssets.Logo).FitArea();
                row.RelativeItem().PaddingTop(10).AlignCenter().Column(title =>
                {
                    title.Item().AlignCenter().Text("TECHNICAL AND COMMERCIAL OFFER")
                        .Bold().FontSize(11.5f).FontColor(PdfTheme.Navy);
                    title.Item().PaddingTop(3).AlignCenter().Text(model.Subject)
                        .FontSize(9.5f).FontColor(PdfTheme.DarkGreen).ClampLines(1);
                });
                row.ConstantItem(150).PaddingTop(6).Column(meta =>
                {
                    MetaRow(meta, "Date", text => text.Span(FormatDate(model.IssuedAt)));
                    MetaRow(meta, "Proposal No.", text => text.Span(model.ProposalNumber));
                    MetaRow(meta, "Page", text => text.CurrentPageNumber());
                });
            });
            column.Item().PaddingTop(7).Height(4.5f).Svg(PdfAssets.StripeSvg(PageWidth - 2 * PdfTheme.PageMargin, 4.5f, 3.5f));
        });

    private static void MetaRow(ColumnDescriptor column, string label, Action<TextDescriptor> value) =>
        column.Item().PaddingBottom(2.5f).Row(row =>
        {
            row.ConstantItem(62).Text(label).Bold().FontSize(8).FontColor(PdfTheme.Muted);
            row.RelativeItem().Text(text =>
            {
                text.DefaultTextStyle(style => style.Bold().FontSize(8.5f).FontColor(PdfTheme.Navy));
                value(text);
            });
        });

    private static void ComposePageFooter(IContainer container, QuotationCompanyProfile company) =>
        container.Column(column =>
        {
            column.Item().Height(1).Background(PdfTheme.Navy);
            column.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(company.Name).Bold().FontSize(8.5f).FontColor(PdfTheme.Navy);
                    left.Item().Text(company.Address).FontSize(8).FontColor(PdfTheme.Muted);
                    left.Item().Text(string.Join("  |  ", company.Phones)).FontSize(8).FontColor(PdfTheme.Muted);
                });
                row.RelativeItem().AlignRight().Column(right =>
                {
                    right.Item().AlignRight().Text(company.Email).FontSize(8).FontColor(PdfTheme.Muted);
                    right.Item().AlignRight().Text(company.Website).FontSize(8).FontColor(PdfTheme.Muted);
                    right.Item().AlignRight().Text("This document is confidential and intended solely for the recipient.")
                        .FontSize(8).FontColor(PdfTheme.Muted);
                });
            });
        });

    private static void SectionBand(ColumnDescriptor column, string title) =>
        column.Item().Background(PdfTheme.Band).CornerRadius(6).Row(row =>
        {
            row.ConstantItem(6).Background(PdfTheme.Green);
            row.RelativeItem().PaddingLeft(10).PaddingVertical(7)
                .Text(title).Bold().FontSize(14).FontColor(PdfTheme.Navy);
        });

    // ---- Sections ---------------------------------------------------------------------------------------------------

    private static void ComposeContents(IContainer container) =>
        container.Column(column =>
        {
            column.Spacing(11);
            SectionBand(column, "Table of Contents");
            column.Item().PaddingTop(4).Column(items =>
            {
                items.Spacing(10);
                for (var index = 0; index < ContentsTitles.Length; index++)
                {
                    var number = index + 1;
                    var title = ContentsTitles[index];
                    items.Item().Border(1).BorderColor(PdfTheme.Line).CornerRadius(8)
                        .PaddingVertical(14).PaddingHorizontal(14).Row(row =>
                        {
                            row.ConstantItem(32).Element(circle => NumberCircle(circle, number, 13, PdfTheme.Navy));
                            row.RelativeItem().AlignMiddle().Text(title).Bold().FontSize(13).FontColor(PdfTheme.Navy);
                        });
                }
            });
        });

    private static void ComposeCoverLetter(IContainer container, QuotationPdfModel model, QuotationCompanyProfile company) =>
        container.Column(column =>
        {
            column.Spacing(11);
            SectionBand(column, "1-Cover Letter:");
            column.Item().PaddingTop(6).Text($"Dear {(string.IsNullOrWhiteSpace(model.ContactName) ? "Sir/Madam" : model.ContactName)},");
            column.Item().Text(text =>
            {
                text.Span("Thank you for your enquiry. ");
                text.Span(company.Name).Bold();
                text.Span(" is pleased to submit this technical and commercial offer to ");
                text.Span(model.ClientName).Bold();
                text.Span(" for ");
                text.Span(model.Subject).Bold();
                text.Span($" (reference {model.ProposalNumber}).");
            });
            column.Item().Text(
                "This document introduces our company and services, sets out the scope of work and deliverables, the price of the work, and the terms and conditions that apply to it.");
            if (!string.IsNullOrWhiteSpace(model.Message))
                column.Item().Text(model.Message);
            column.Item().Text(
                "We trust the offer meets your requirements and we look forward to working with you. Please do not hesitate to contact us if you need any clarification.");
            column.Item().PaddingTop(14).Text("Yours sincerely,");
            column.Item().PaddingTop(16).Column(signature =>
            {
                signature.Item().Text(model.PreparedBy).Bold().FontSize(12).FontColor(PdfTheme.Navy);
                signature.Item().Text(model.PreparedByTitle).FontColor(PdfTheme.Muted);
                signature.Item().Text(company.Name).FontColor(PdfTheme.Muted);
            });
        });

    private static void ComposeAboutAndVision(IContainer container, QuotationCompanyProfile company) =>
        container.Column(column =>
        {
            column.Spacing(11);
            SectionBand(column, "2-About Us:");
            column.Item().PaddingBottom(14).Text(text => Rich(text, company.AboutUs));
            SectionBand(column, "3-Our Vision:");
            column.Item().Text(text => Rich(text, company.Vision));
        });

    private static void ComposeServices(IContainer container, QuotationServiceSection section) =>
        container.Column(column =>
        {
            column.Spacing(9);
            SectionBand(column, "4-Integrity Services:");
            column.Item().Text(section.Title).Bold().FontSize(13).FontColor(PdfTheme.DarkGreen);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(46);
                    columns.RelativeColumn(54);
                });
                table.Header(header =>
                {
                    HeaderCell(header.Cell(), section.ServiceColumn);
                    HeaderCell(header.Cell(), section.ScopeColumn);
                });

                var number = 0;
                var rowIndex = 0;
                foreach (var group in section.Groups)
                {
                    if (group.Heading is not null)
                    {
                        table.Cell().ColumnSpan(2).Background(PdfTheme.Band)
                            .BorderBottom(0.5f).BorderColor(PdfTheme.Line)
                            .PaddingVertical(3.5f).PaddingHorizontal(8)
                            .Text(group.Heading).Bold().FontSize(9.5f).FontColor(PdfTheme.DarkGreen);
                        rowIndex = 0;
                    }

                    foreach (var line in group.Lines)
                    {
                        var alternate = rowIndex++ % 2 == 1;
                        var label = section.Numbered ? $"{++number}-  {line.Service}" : line.Service;
                        BodyCell(table.Cell(), alternate, 3.2f).Text(label).Bold().FontSize(9).LineHeight(1.2f).FontColor(PdfTheme.Navy);
                        BodyCell(table.Cell(), alternate, 3.2f).Text(line.Scope).FontSize(9).LineHeight(1.2f);
                    }
                }
            });
        });

    private static void ComposeScope(IContainer container, QuotationPdfModel model) =>
        container.Column(column =>
        {
            column.Spacing(10);
            SectionBand(column, "5-Scope of work:");
            column.Item().PaddingTop(2).Column(list =>
            {
                list.Spacing(7);
                for (var index = 0; index < model.Items.Count; index++)
                {
                    var number = index + 1;
                    var item = model.Items[index];
                    list.Item().PaddingLeft(14).Row(row =>
                    {
                        row.ConstantItem(26).Element(circle => NumberCircle(circle, number, 9, PdfTheme.Navy, 18));
                        row.RelativeItem().AlignMiddle().Text(item.Type).FontSize(11);
                    });
                }
            });

            column.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(17);
                    columns.RelativeColumn(46);
                    columns.RelativeColumn(12);
                    columns.RelativeColumn(25);
                });
                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "PR number", center: true);
                    HeaderCell(header.Cell(), "Description");
                    HeaderCell(header.Cell(), "Qty", center: true);
                    HeaderCell(header.Cell(), "Unit of Measure", center: true);
                });
                for (var index = 0; index < model.Items.Count; index++)
                {
                    var item = model.Items[index];
                    var alternate = index % 2 == 1;
                    BodyCell(table.Cell(), alternate, 7).AlignCenter().Text(item.PrNumber ?? "—");
                    BodyCell(table.Cell(), alternate, 7).Text(item.Description);
                    BodyCell(table.Cell(), alternate, 7).AlignCenter().Text(Format(item.Quantity));
                    BodyCell(table.Cell(), alternate, 7).AlignCenter().Text(QuotationDefaults.UnitOfMeasure);
                }
            });

            if (model.ScopeOfWork.Count > 0)
                column.Item().PaddingTop(10).Element(content => ComposePoints(content, "The visit covers", model.ScopeOfWork));
        });

    private static void ComposePoints(IContainer container, string title, IReadOnlyList<string> points) =>
        container.Column(column =>
        {
            column.Spacing(4);
            column.Item().Text(title).Bold().FontSize(11).FontColor(PdfTheme.Navy);
            foreach (var point in points)
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(16).PaddingTop(3).Width(8).Height(8).Svg(PdfAssets.TriangleBulletSvg);
                    row.RelativeItem().Text(point);
                });
            }
        });

    private static void ComposeBudget(IContainer container, QuotationPdfModel model, QuotationCompanyProfile company) =>
        container.Column(column =>
        {
            column.Spacing(10);
            SectionBand(column, "6-Project Budget:");
            column.Item().PaddingTop(2).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(8);
                    columns.RelativeColumn(34);
                    columns.RelativeColumn(14);
                    columns.RelativeColumn(17);
                    columns.RelativeColumn(8);
                    columns.RelativeColumn(19);
                });
                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "Item", center: true);
                    HeaderCell(header.Cell(), "Description", center: true);
                    HeaderCell(header.Cell(), "Unit of Pricing", center: true);
                    HeaderCell(header.Cell(), "Cost Per Item (EGP)", center: true);
                    HeaderCell(header.Cell(), "Qty", center: true);
                    HeaderCell(header.Cell(), "Total Cost (EGP)", center: true);
                });
                for (var index = 0; index < model.Items.Count; index++)
                {
                    var item = model.Items[index];
                    var alternate = index % 2 == 1;
                    BodyCell(table.Cell(), alternate, 8).AlignCenter().Text(Format(index + 1));
                    BodyCell(table.Cell(), alternate, 8).Row(row =>
                    {
                        row.ConstantItem(12).PaddingTop(4).Width(4).Height(4).Svg(PdfAssets.TriangleBulletSvg.Replace("polygon points=\"1,0.5 7,4 1,7.5\"", "circle cx=\"4\" cy=\"4\" r=\"3\""));
                        row.RelativeItem().Text(item.Description);
                    });
                    BodyCell(table.Cell(), alternate, 8).AlignCenter().Text(item.PricingUnit);
                    BodyCell(table.Cell(), alternate, 8).AlignCenter().Text(FormatMoney(item.UnitPricePiastres));
                    BodyCell(table.Cell(), alternate, 8).AlignCenter().Text(Format(item.Quantity));
                    BodyCell(table.Cell(), alternate, 8).AlignCenter().Text(FormatMoney(item.TotalPiastres));
                }

                var total = model.Items.Aggregate(0L, (sum, item) => checked(sum + (item.TotalPiastres ?? 0)));
                table.Cell().ColumnSpan(4).Background(PdfTheme.Band).Padding(7).Text(string.Empty);
                table.Cell().Background(PdfTheme.Band).PaddingVertical(7).AlignCenter().Text("Total").Bold();
                table.Cell().Background(PdfTheme.Band).PaddingVertical(7).AlignCenter().Text(FormatMoney(total)).Bold();
            });

            column.Item().Background(PdfTheme.DarkGreen).CornerRadius(5).PaddingVertical(7).AlignCenter()
                .Text($"{QuotationDefaults.VatRatePercent}% VAT will be added")
                .Bold().FontColor(PdfTheme.White);

            var notes = model.Notes.Append(QuotationDefaults.ValidityNote(model.ValidForDays)).ToList();
            column.Item().PaddingTop(8).Element(content => ComposeNotes(content, notes));
            _ = company;
        });

    private static void ComposeNotes(IContainer container, IReadOnlyList<string> notes) =>
        container.Border(1).BorderColor(PdfTheme.Line).Column(column =>
        {
            column.Item().Background(PdfTheme.Navy).PaddingVertical(8).PaddingHorizontal(10).Row(row =>
            {
                row.ConstantItem(8).PaddingTop(1).Width(4).Height(12).Background(PdfTheme.Green);
                row.RelativeItem().PaddingLeft(4).Text("NOTE THAT").Bold().FontSize(11).LetterSpacing(0.1f).FontColor(PdfTheme.White);
            });
            for (var index = 0; index < notes.Count; index++)
            {
                var number = index + 1;
                var note = notes[index];
                column.Item().Background(index % 2 == 1 ? PdfTheme.RowAlt : PdfTheme.White)
                    .PaddingVertical(5).PaddingHorizontal(10).Row(row =>
                    {
                        row.ConstantItem(26).Element(circle => NumberCircle(circle, number, 7.5f, PdfTheme.Green, 15));
                        row.RelativeItem().Text(text => Rich(text, EmphasizeLeadIn(note)));
                    });
            }
        });

    private static void ComposeTerms(IContainer container, QuotationPdfModel model, QuotationCompanyProfile company) =>
        container.Column(column =>
        {
            column.Spacing(8);
            SectionBand(column, "7-Terms and conditions:");
            foreach (var section in company.Terms)
            {
                column.Item().PaddingTop(4).Column(block =>
                {
                    block.Spacing(3);
                    block.Item().Text(section.Title).Bold().FontSize(12).FontColor(PdfTheme.Navy);
                    foreach (var point in section.Points)
                    {
                        var text = point.Replace("{days}", model.ValidForDays.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
                        block.Item().PaddingLeft(12).Row(row =>
                        {
                            row.ConstantItem(16).PaddingTop(3).Width(8).Height(8).Svg(PdfAssets.TriangleBulletSvg);
                            row.RelativeItem().Text(content => Rich(content, text));
                        });
                    }

                    if (section.ShowBankAccounts)
                        block.Item().PaddingTop(2).Element(content => ComposeBankAccounts(content, company.BankAccounts));
                });
            }

            column.Item().PaddingTop(10).AlignCenter().Text("==== End of Quotation ====").Bold().FontSize(13).FontColor(PdfTheme.Navy);
        });

    private static void ComposeBankAccounts(IContainer container, IReadOnlyList<QuotationBankAccount> accounts) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(16);
                columns.RelativeColumn(17);
                columns.RelativeColumn(11);
                columns.RelativeColumn(15);
                columns.RelativeColumn(26);
                columns.RelativeColumn(15);
            });
            table.Header(header =>
            {
                foreach (var title in new[] { "Beneficiary Name", "Bank Name", "Currency", "Account Number", "IBAN", "SWIFT Code" })
                    HeaderCell(header.Cell(), title, fontSize: 8.5f);
            });
            for (var index = 0; index < accounts.Count; index++)
            {
                var account = accounts[index];
                var alternate = index % 2 == 1;
                foreach (var value in new[] { account.Beneficiary, account.Bank, account.Currency, account.AccountNumber, account.Iban, account.Swift })
                    BodyCell(table.Cell(), alternate, 5).Text(value).FontSize(8.5f);
            }
        });

    private static void ComposeThankYou(IDocumentContainer document, QuotationCompanyProfile company) =>
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.PageColor(PdfTheme.White);
            page.DefaultTextStyle(style => style.FontFamily(PdfTheme.FontFamily).FontColor(PdfTheme.Navy));
            page.Background().Svg(PdfAssets.GridSvg(PageWidth, PageHeight));
            page.Content().PaddingTop(236).AlignCenter().Column(column =>
            {
                column.Item().AlignCenter().Width(96).Image(PdfAssets.Logo).FitWidth();
                column.Item().PaddingTop(30).AlignCenter().Text("THANK YOU")
                    .Bold().FontSize(46).LetterSpacing(0.14f).FontColor(PdfTheme.Navy);
                column.Item().PaddingTop(14).AlignCenter().Row(row =>
                {
                    row.ConstantItem(46).Height(3.5f).Background(PdfTheme.Green);
                    row.ConstantItem(10);
                    row.ConstantItem(46).Height(3.5f).Background(PdfTheme.Navy);
                });
                column.Item().PaddingTop(22).AlignCenter()
                    .Text("We appreciate the opportunity to work with you.").FontSize(12.5f).FontColor(PdfTheme.Muted);
                column.Item().PaddingTop(30).AlignCenter().Text(company.Name).Bold().FontSize(11);
                column.Item().PaddingTop(4).AlignCenter().Text(company.Address).FontSize(9.5f).FontColor(PdfTheme.Muted);
                column.Item().AlignCenter().Text(string.Join("  |  ", company.Phones)).FontSize(9.5f).FontColor(PdfTheme.Muted);
                column.Item().AlignCenter().Text($"{company.Email}  |  {company.Website}").FontSize(9.5f).FontColor(PdfTheme.Muted);
            });
            page.Footer().Height(14).Svg(PdfAssets.StripeSvg(PageWidth, 14, 20));
        });

    // ---- Building blocks --------------------------------------------------------------------------------------------

    private static void HeaderCell(IContainer cell, string text, bool center = false, float fontSize = 9.5f)
    {
        var padded = cell.Background(PdfTheme.Navy).PaddingVertical(7).PaddingHorizontal(8);
        var aligned = center ? padded.AlignCenter() : padded;
        aligned.Text(text).Bold().FontSize(fontSize).FontColor(PdfTheme.White);
    }

    private static IContainer BodyCell(IContainer cell, bool alternate, float verticalPadding = 5) =>
        cell.Background(alternate ? PdfTheme.RowAlt : PdfTheme.White)
            .BorderBottom(0.5f).BorderColor(PdfTheme.Line)
            .PaddingVertical(verticalPadding).PaddingHorizontal(8);

    private static void NumberCircle(IContainer container, int number, float fontSize, Color color, float diameter = 22) =>
        container.Width(diameter).Height(diameter).Background(color).CornerRadius(diameter / 2)
            .AlignCenter().AlignMiddle()
            .Text(Format(number)).Bold().FontSize(fontSize).FontColor(PdfTheme.White);

    // Text between ** markers is bold.
    private static void Rich(TextDescriptor text, string markup)
    {
        var parts = markup.Split("**");
        for (var index = 0; index < parts.Length; index++)
        {
            if (parts[index].Length == 0) continue;
            var span = text.Span(parts[index]);
            if (index % 2 == 1) span.Bold();
        }
    }

    // "Payment terms: 90 days..." becomes "**Payment terms:** 90 days...".
    private static string EmphasizeLeadIn(string note)
    {
        var colon = note.IndexOf(':', StringComparison.Ordinal);
        if (colon is < 3 or > 36 || note.Contains("**", StringComparison.Ordinal)) return note;
        var lead = note[..(colon + 1)];
        return lead.All(character => char.IsLetter(character) || character is ' ' or '/' or '-' or ':')
            ? $"**{lead}**{note[(colon + 1)..]}"
            : note;
    }

    private static float FitFontSize(string text, float maxWidth, float maxSize, float minSize, float charWidth)
    {
        var length = Math.Max(text.Length, 1);
        return Math.Clamp(maxWidth / (length * charWidth), minSize, maxSize);
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

    // Prices are stored in piastres. Whole amounts print without decimals ("12,000 EGP"); an item without a price prints as a dash.
    private static string FormatMoney(long? piastres)
    {
        if (piastres is not long value) return "—";
        var format = value % 100 == 0 ? "N0" : "N2";
        return (value / 100m).ToString(format, CultureInfo.InvariantCulture) + " EGP";
    }

    private static string SafeFileName(string value)
    {
        var cleaned = new string(value.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-').ToArray()).Trim('-');
        return cleaned.Length == 0 ? "offer" : cleaned;
    }
}
