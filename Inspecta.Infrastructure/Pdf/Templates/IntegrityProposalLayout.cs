using System.Globalization;
using System.Text.RegularExpressions;
using Inspecta.Application.Pdf;
using Inspecta.Application.Pdf.Models;
using Inspecta.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Inspecta.Infrastructure.Pdf.Templates;

/// <summary>
/// The technical and commercial proposal in the design Integrity International for Inspection made for its own quotations: a cover with the
/// blue-to-green band, a framed page (left strip, header with the proposal number, faded logo, contact footer), contents, cover letter,
/// about us and vision, the service tables, the accreditation certificate, scope of work, technical methodology, cost and pricing and terms.
/// It is used only by the built-in identity; every other company keeps the layout in <see cref="QuotationPdfTemplate"/>.
/// </summary>
internal static class IntegrityProposalLayout
{
    private static readonly Color Navy = Color.FromHex("#1E3A78");
    private static readonly Color Blue = Color.FromHex("#2F6DB5");
    private static readonly Color Green = Color.FromHex("#2E9B43");
    private static readonly Color DarkGreen = Color.FromHex("#1F7A4A");
    private static readonly Color DeepGreen = Color.FromHex("#0F4D2A");
    private static readonly Color BandBlue = Color.FromHex("#E9F0FA");
    private static readonly Color BandGreen = Color.FromHex("#E2EFD9");
    private static readonly Color RowTint = Color.FromHex("#F5F8FD");
    private static readonly Color RowGreen = Color.FromHex("#EAF5EC");
    private static readonly Color Line = Color.FromHex("#D5DFEE");
    private static readonly Color Text = Color.FromHex("#2C3D33");
    private static readonly Color Muted = Color.FromHex("#51607A");
    private static readonly Color White = Color.FromHex("#FFFFFF");

    // Images used on many pages are loaded once per document and shared by its pages, so each is stored once in the PDF and the file stays
    // small enough to email. They are made for each document rather than kept in statics, so two documents drawn at once share nothing.
    private sealed record Pictures(Image Logo, Image Strip, Image Watermark);

    // The proposal prints the lists of the company details; a profile that only has the single address or email still prints it.
    private static IReadOnlyList<string> AddressesOf(QuotationCompanyProfile company) =>
        company.Addresses ?? (string.IsNullOrWhiteSpace(company.Address) ? [] : [company.Address]);

    private static IReadOnlyList<string> EmailsOf(QuotationCompanyProfile company) =>
        company.Emails ?? (string.IsNullOrWhiteSpace(company.Email) ? [] : [company.Email]);


    private const string CirclesSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"595\" height=\"842\" viewBox=\"0 0 595 842\">" +
        "<circle cx=\"548\" cy=\"6\" r=\"112\" fill=\"#E9F0FA\"/>" +
        "<circle cx=\"556\" cy=\"-2\" r=\"84\" fill=\"#E2EFD9\" fill-opacity=\"0.85\"/>" +
        "<circle cx=\"592\" cy=\"2\" r=\"64\" fill=\"#C3D5EC\" stroke=\"#2E9B43\" stroke-width=\"0.7\"/>" +
        "</svg>";

    private sealed record Section(string Key, string Title, string Subtitle);

    private static IReadOnlyList<Section> SectionsOf(QuotationCompanyProfile company)
    {
        var sections = new List<Section> { new("cover", "Cover Letter", "Our introduction and covering note to your team") };
        if (!string.IsNullOrWhiteSpace(company.AboutUs)) sections.Add(new("about", "About Us", "Who we are and how we work"));
        if (!string.IsNullOrWhiteSpace(company.Vision)) sections.Add(new("vision", "Our Vision", "Where we are heading and what we stand for"));
        if (company.Services.Count > 0) sections.Add(new("services", company.ServicesLabel, "Industrial and environmental services we provide"));
        sections.Add(new("scope", "Scope of Work", "What is included in this quotation"));
        sections.Add(new("method", "Technical Methodology", "How the work is planned, performed and reported"));
        sections.Add(new("pricing", "Cost and Pricing", "Itemized price and totals"));
        if (company.Terms.Count > 0) sections.Add(new("terms", "Terms and Conditions", "Invoicing, payment, bank details and obligations"));
        return sections;
    }

    private static int NumberOf(IReadOnlyList<Section> sections, string key) => sections.ToList().FindIndex(section => section.Key == key) + 1;

    public static void Compose(IDocumentContainer document, QuotationPdfModel model, QuotationCompanyProfile company)
    {
        var sections = SectionsOf(company);
        var pictures = new Pictures(
            Image.FromBinaryData(PdfAssets.ProposalLogo), Image.FromBinaryData(PdfAssets.ProposalStrip), Image.FromBinaryData(PdfAssets.ProposalWatermark));

        ComposeCover(document, model, company, pictures);
        Page(document, model, company, pictures, content => ComposeContents(content, sections));
        Page(document, model, company, pictures, content => ComposeCoverLetter(content, model, company, sections));
        if (sections.Any(section => section.Key is "about" or "vision"))
            Page(document, model, company, pictures, content => ComposeAboutAndVision(content, company, sections));
        for (var index = 0; index < company.Services.Count; index++)
        {
            var section = company.Services[index];
            var position = index + 1;
            Page(document, model, company, pictures, content => ComposeServices(content, section, company.ServicesLabel, NumberOf(sections, "services"), position, position == 1));
        }

        Page(document, model, company, pictures, content => content.AlignCenter().AlignMiddle().Image(PdfAssets.ProposalAccreditation).FitArea());
        Page(document, model, company, pictures, content => ComposeScope(content, model, sections));
        Page(document, model, company, pictures, content => ComposeMethodology(content, sections));
        Page(document, model, company, pictures, content => ComposePricing(content, model, sections));
        if (company.Terms.Count > 0)
            Page(document, model, company, pictures, content => ComposeTerms(content, model, company, sections));
    }

    // ---- Cover -------------------------------------------------------------------------------------------------

    private static void ComposeCover(IDocumentContainer document, QuotationPdfModel model, QuotationCompanyProfile company, Pictures pictures) =>
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.PageColor(White);
            page.DefaultTextStyle(style => style.FontFamily(PdfTheme.FontFamily).FontColor(Navy));
            page.Content().Column(column =>
            {
                column.Item().Height(180).Layers(layers =>
                {
                    layers.PrimaryLayer().Background(White);
                    layers.Layer().OffsetX(52).OffsetY(28).Width(150).Height(140).Image(pictures.Logo).FitArea();
                    layers.Layer().OffsetX(310).OffsetY(56).Width(235).Column(meta =>
                    {
                        meta.Spacing(12);
                        CoverMeta(meta, "PROPOSAL NO.", model.ProposalNumber);
                        CoverMeta(meta, "DATE", QuotationPdfTemplate.FormatDate(model.IssuedAt));
                        CoverMeta(meta, "VALID FOR", $"{model.ValidForDays} days");
                    });
                });

                column.Item().Height(300).Layers(layers =>
                {
                    layers.PrimaryLayer().Image(PdfAssets.ProposalBand).FitUnproportionally();
                    layers.Layer().OffsetX(78).OffsetY(62).Text("TECHNICAL & COMMERCIAL PROPOSAL")
                        .Bold().FontSize(10.5f).LetterSpacing(0.3f).FontColor(Color.FromHex("#B4E4BC"));
                    layers.Layer().OffsetX(76).OffsetY(78).Text("QUOTATION").Bold().FontSize(60).FontColor(White);
                    layers.Layer().OffsetX(78).OffsetY(170).Width(70).Height(3).Background(Color.FromHex("#8FD39C"));
                    layers.Layer().OffsetX(78).OffsetY(184).Text("for Inspection Services").FontSize(17).FontColor(White);
                    layers.Layer().OffsetX(228).OffsetY(268).Text("Integrity in Every Inspection. Confidence in Every Result.")
                        .FontSize(11.5f).FontColor(White);
                });

                column.Item().PaddingTop(26).PaddingHorizontal(50).Column(info =>
                {
                    CoverInfo(info, "PREPARED FOR", model.ClientName, tinted: true);
                    CoverInfo(info, "ATTENTION", string.IsNullOrWhiteSpace(model.ContactName) ? "—" : model.ContactName, tinted: false);
                    CoverInfo(info, "SUBJECT", model.Subject, tinted: true);
                });

                column.Item().PaddingTop(22).PaddingHorizontal(50).Row(row =>
                {
                    CoverContact(row, "PHONE", Green, RowGreen, company.Phones);
                    CoverContact(row, "EMAIL", Blue, BandBlue, EmailsOf(company));
                    CoverContact(row, "ADDRESS", Green, RowGreen, AddressesOf(company));
                });

                column.Item().PaddingTop(18).PaddingHorizontal(50).Column(bar =>
                {
                    bar.Item().Height(6).Background(Green);
                    bar.Item().Background(Navy).PaddingVertical(16).PaddingHorizontal(10).AlignCenter()
                        .Text(company.Name.ToUpperInvariant()).Bold().FontSize(14).LetterSpacing(0.2f).FontColor(White).ClampLines(2);
                });
            });
        });

    private static void CoverMeta(ColumnDescriptor column, string label, string value) =>
        column.Item().Row(row =>
        {
            row.ConstantItem(92).PaddingTop(2).Text(label).Bold().FontSize(7.5f).LetterSpacing(0.15f).FontColor(Navy);
            row.RelativeItem().BorderBottom(0.7f).BorderColor(Color.FromHex("#9DB2D9")).PaddingBottom(3)
                .Text(value).FontSize(10).FontColor(Text).ClampLines(1);
        });

    private static void CoverInfo(ColumnDescriptor column, string label, string value, bool tinted) =>
        column.Item().BorderBottom(0.6f).BorderColor(Color.FromHex("#C5D3EA")).Row(row =>
        {
            row.ConstantItem(100).Background(Color.FromHex("#D9E2F3")).PaddingVertical(8).PaddingHorizontal(8)
                .Text(label).Bold().FontSize(8).LetterSpacing(0.12f).FontColor(Navy);
            row.RelativeItem().Background(tinted ? Color.FromHex("#D9E2F3") : White).PaddingVertical(8).PaddingHorizontal(8)
                .Text(value).Bold().FontSize(10).FontColor(Navy).ClampLines(2);
        });

    private static void CoverContact(RowDescriptor row, string label, Color accent, Color tint, IReadOnlyList<string> lines) =>
        row.RelativeItem().BorderTop(3).BorderColor(accent).Background(tint).Padding(9).Column(cell =>
        {
            cell.Item().PaddingBottom(4).Text(label).Bold().FontSize(7.5f).LetterSpacing(0.3f).FontColor(accent);
            // A line longer than the cell is cut after two lines: the footer of every page carries the full value.
            foreach (var line in lines) cell.Item().Text(line).FontSize(8).FontColor(Text).ClampLines(2);
        });

    // ---- Page frame --------------------------------------------------------------------------------------------

    private static void Page(
        IDocumentContainer document,
        QuotationPdfModel model,
        QuotationCompanyProfile company,
        Pictures pictures,
        Action<IContainer> content) =>
        document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginTop(22);
            page.MarginBottom(16);
            page.MarginLeft(46);
            page.MarginRight(46);
            page.PageColor(White);
            page.DefaultTextStyle(style => style.FontFamily(PdfTheme.FontFamily).FontSize(10).FontColor(Text).LineHeight(1.35f));
            page.Background().Layers(layers =>
            {
                layers.PrimaryLayer().Svg(CirclesSvg);
                layers.Layer().AlignLeft().Width(11).Image(pictures.Strip).FitUnproportionally();
                layers.Layer().AlignCenter().AlignMiddle().Width(340).Image(pictures.Watermark).FitWidth();
            });
            page.Header().Element(header => ComposeHeader(header, model, pictures));
            page.Content().PaddingTop(14).PaddingBottom(8).Element(content);
            page.Footer().Element(footer => ComposeFooter(footer, company));
        });

    private static void ComposeHeader(IContainer container, QuotationPdfModel model, Pictures pictures) =>
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(84).Height(58).Image(pictures.Logo).FitArea();
                row.RelativeItem().PaddingTop(12).PaddingHorizontal(8).Column(title =>
                {
                    title.Item().AlignCenter().Text("Technical and Commercial Proposal for").Bold().FontSize(10).FontColor(Navy);
                    title.Item().AlignCenter().Text(model.Subject).Bold().FontSize(10).FontColor(Green).ClampLines(2);
                });
                row.ConstantItem(150).PaddingTop(8).Column(meta =>
                {
                    MetaRow(meta, "Date:", value => value.Span(QuotationPdfTemplate.FormatDate(model.IssuedAt)));
                    MetaRow(meta, "Proposal No.:", value => value.Span(model.ProposalNumber));
                    MetaRow(meta, "Page number:", value =>
                    {
                        value.Span("Page ");
                        value.CurrentPageNumber();
                        value.Span(" of ");
                        value.TotalPages();
                    });
                });
            });
            column.Item().PaddingTop(4).Height(1.2f).Background(Blue);
        });

    private static void MetaRow(ColumnDescriptor column, string label, Action<TextDescriptor> value) =>
        column.Item().PaddingBottom(2.5f).Row(row =>
        {
            row.ConstantItem(66).Text(label).Bold().FontSize(8).FontColor(Muted);
            row.RelativeItem().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8.5f).FontColor(Text));
                value(text);
            });
        });

    private static void ComposeFooter(IContainer container, QuotationCompanyProfile company) =>
        container.Column(column =>
        {
            column.Item().Background(Navy).Column(block =>
            {
                block.Item().PaddingHorizontal(6).PaddingVertical(3).Text(company.Name.ToUpperInvariant())
                    .Bold().FontSize(7.5f).LetterSpacing(0.2f).FontColor(White).ClampLines(1);
                FooterLine(block, string.Join("  /  ", AddressesOf(company)));
                FooterLine(block, string.Join("  |  ", company.Phones));
                foreach (var email in EmailsOf(company)) FooterLine(block, email);
                block.Item().BorderTop(0.4f).BorderColor(Color.FromHex("#4A66A6")).PaddingHorizontal(6).PaddingVertical(2).Row(row =>
                {
                    row.RelativeItem().Text(company.Website).FontSize(7).FontColor(White).ClampLines(1);
                    row.AutoItem().Text("Confidential").Bold().FontSize(6.5f).FontColor(White);
                });
            });
            column.Item().Height(3).Background(Green);
        });

    private static void FooterLine(ColumnDescriptor column, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        column.Item().BorderTop(0.4f).BorderColor(Color.FromHex("#4A66A6")).PaddingHorizontal(6).PaddingVertical(2)
            .Text(text).FontSize(7).FontColor(White).ClampLines(2);
    }

    // ---- Building blocks ---------------------------------------------------------------------------------------

    private static void SectionTitle(ColumnDescriptor column, string key, int number, string title) =>
        column.Item().Section(key).Column(block =>
        {
            block.Item().Text($"SECTION {number.ToString("00", CultureInfo.InvariantCulture)}")
                .Bold().FontSize(8).LetterSpacing(0.35f).FontColor(Blue);
            block.Item().Text(title).Bold().FontSize(26).FontColor(Navy).LineHeight(1.1f);
            block.Item().PaddingTop(5).Row(row =>
            {
                row.ConstantItem(46).Height(3).Background(Blue);
                row.ConstantItem(46).Height(3).Background(Green);
            });
        });

    private static void SubBand(ColumnDescriptor column, string label, string title, bool navy) =>
        column.Item().Row(row =>
        {
            row.ConstantItem(42).Background(navy ? Navy : Green).PaddingVertical(8).AlignCenter()
                .Text(label).Bold().FontSize(10).FontColor(White);
            row.RelativeItem().Background(navy ? BandBlue : BandGreen).PaddingVertical(8).PaddingLeft(14)
                .Text(title).Bold().FontSize(13).FontColor(navy ? Navy : Green);
        });

    private static void Bullet(ColumnDescriptor column, string markup, float size = 10) =>
        column.Item().PaddingLeft(8).Row(row =>
        {
            row.ConstantItem(14).PaddingTop(size * 0.42f).Width(4).Height(4).Background(Green);
            row.RelativeItem().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(size));
                QuotationPdfTemplate.Rich(text, markup);
            });
        });

    private static void HeaderCell(IContainer cell, string text, bool center = false)
    {
        var padded = cell.Background(Navy).PaddingVertical(6.5f).PaddingHorizontal(9);
        (center ? padded.AlignCenter() : padded).Text(text.ToUpperInvariant()).Bold().FontSize(8).LetterSpacing(0.12f).FontColor(White);
    }

    private static IContainer BodyCell(IContainer cell, bool alternate, float verticalPadding = 3f) =>
        cell.Background(alternate ? RowTint : White).BorderBottom(0.4f).BorderColor(Line)
            .PaddingVertical(verticalPadding).PaddingHorizontal(9);

    // "4-1 Industrial Services" and "7.1 Invoicing" are numbered again by position, so the stored number is dropped.
    private static string WithoutNumber(string title) =>
        Regex.Replace(title, @"^\s*\d+([-.]\d+)*\s*[-–:.]?\s*", string.Empty, RegexOptions.None, TimeSpan.FromMilliseconds(100)).Trim();

    // ---- Sections ----------------------------------------------------------------------------------------------

    private static void ComposeContents(IContainer container, IReadOnlyList<Section> sections) =>
        container.Column(column =>
        {
            column.Item().Text("INTRODUCTION").Bold().FontSize(8).LetterSpacing(0.35f).FontColor(Blue);
            column.Item().Text("Contents").Bold().FontSize(28).FontColor(Navy).LineHeight(1.1f);
            column.Item().PaddingTop(5).Row(row =>
            {
                row.ConstantItem(46).Height(3).Background(Blue);
                row.ConstantItem(46).Height(3).Background(Green);
            });
            column.Item().PaddingTop(20).Column(rows =>
            {
                for (var index = 0; index < sections.Count; index++)
                {
                    var section = sections[index];
                    var number = index + 1;
                    var color = number % 2 == 1 ? Navy : Green;
                    rows.Item().BorderBottom(0.6f).BorderColor(Line).PaddingVertical(9).Row(row =>
                    {
                        row.ConstantItem(46).Width(34).Height(34).Background(color).AlignCenter().AlignMiddle()
                            .Text(number.ToString("00", CultureInfo.InvariantCulture)).Bold().FontSize(13).FontColor(White);
                        row.RelativeItem().AlignMiddle().Column(title =>
                        {
                            title.Item().Text(section.Title).Bold().FontSize(12.5f).FontColor(Navy);
                            title.Item().Text(section.Subtitle).FontSize(9).FontColor(Muted);
                        });
                        row.ConstantItem(36).AlignRight().AlignMiddle().Text(text => text.BeginPageNumberOfSection(section.Key));
                    });
                }
            });
        });

    private static void ComposeCoverLetter(IContainer container, QuotationPdfModel model, QuotationCompanyProfile company, IReadOnlyList<Section> sections) =>
        container.Column(column =>
        {
            column.Spacing(10);
            SectionTitle(column, "cover", NumberOf(sections, "cover"), "Cover Letter");
            column.Item().PaddingTop(8).Column(address =>
            {
                address.Item().Text(model.ClientName).Bold().FontSize(11).FontColor(Navy).ClampLines(2);
                if (!string.IsNullOrWhiteSpace(model.ContactName))
                    address.Item().Text(text =>
                    {
                        text.Span("Attention: ").Bold();
                        text.Span(model.ContactName);
                    });
            });
            column.Item().BorderTop(2).BorderColor(Blue).Background(BandBlue).PaddingVertical(9).PaddingHorizontal(14).Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(11).Bold().FontColor(Navy));
                text.Span("Subject: ");
                text.Span($"Technical and commercial proposal for {model.Subject}");
            });
            column.Item().PaddingTop(8).Text($"Dear {(string.IsNullOrWhiteSpace(model.ContactName) ? "Valued Customer" : model.ContactName)},");
            column.Item().Text(
                $"Thank you for giving {company.Name} the opportunity to submit this proposal for {model.Subject} (reference {model.ProposalNumber}). " +
                "We have reviewed your requirements and prepared a technical and commercial proposal suited to your facility and schedule.");
            column.Item().Text(
                "This document introduces our company and vision, the services we provide, the proposed scope of work, our technical methodology, " +
                "the cost and pricing, and the terms and conditions that apply. Every inspection is planned and carried out with a focus on safety, " +
                "accuracy and clear, well-documented reporting.");
            if (!string.IsNullOrWhiteSpace(model.Message))
                column.Item().Text(model.Message);
            column.Item().Text(
                "We trust this offer meets your expectations. Please contact us if you need any clarification or would like to adjust the scope, " +
                "and we will be glad to update the proposal accordingly. We look forward to working with you.");
            column.Item().PaddingTop(10).Text("Yours sincerely,");
            column.Item().PaddingTop(14).Width(230).BorderTop(1.2f).BorderColor(Navy).PaddingTop(6).Column(signature =>
            {
                signature.Item().Text(model.PreparedBy).Bold().FontSize(11.5f).FontColor(Navy).ClampLines(2);
                foreach (var line in QuotationPdfTemplate.SignatureLines(model)) signature.Item().Text(line).ClampLines(2);
            });
            column.Item().PaddingTop(8).Text(company.Name.ToUpperInvariant()).Bold().FontSize(8.5f).LetterSpacing(0.25f).FontColor(DarkGreen);
        });

    private static void ComposeAboutAndVision(IContainer container, QuotationCompanyProfile company, IReadOnlyList<Section> sections) =>
        container.Column(column =>
        {
            column.Spacing(11);
            if (!string.IsNullOrWhiteSpace(company.AboutUs))
            {
                SectionTitle(column, "about", NumberOf(sections, "about"), "About Us");
                column.Item().PaddingTop(4).Text(text => QuotationPdfTemplate.Rich(text, company.AboutUs));
                column.Item().PaddingTop(4).Row(row =>
                {
                    row.Spacing(10);
                    Card(row, "Independent", "Impartial findings, reported exactly as measured.", Green);
                    Card(row, "Qualified", "Experienced engineers and inspectors on every job.", Blue);
                    Card(row, "Standards-based", "Work planned against recognized codes and procedures.", Green);
                });
            }

            if (!string.IsNullOrWhiteSpace(company.Vision))
            {
                SectionTitle(column, "vision", NumberOf(sections, "vision"), "Our Vision");
                column.Item().PaddingTop(6).Background(Navy).Padding(18).Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(12).Bold().FontColor(White).LineHeight(1.45f));
                    QuotationPdfTemplate.Rich(text, company.Vision);
                });
                column.Item().Text("INTEGRITY IN EVERY INSPECTION").Bold().FontSize(9).LetterSpacing(0.25f).FontColor(Green);
                column.Item().Height(3).Background(Green);
            }
        });

    private static void Card(RowDescriptor row, string title, string body, Color accent) =>
        row.RelativeItem().BorderTop(2.5f).BorderColor(accent).Background(RowTint).Padding(10).Column(card =>
        {
            card.Item().PaddingBottom(3).Text(title).Bold().FontSize(11).FontColor(Navy);
            card.Item().Text(body).FontSize(9).FontColor(Muted);
        });

    private static void ComposeServices(IContainer container, QuotationServiceSection section, string servicesLabel, int sectionNumber, int position, bool first) =>
        container.Column(column =>
        {
            column.Spacing(7);
            if (first) SectionTitle(column, "services", sectionNumber, servicesLabel);
            SubBand(column, $"{sectionNumber}-{position}", WithoutNumber(section.Title), navy: position % 2 == 1);

            if (section.Groups.Any(group => group.Heading is not null))
                column.Item().PaddingTop(0).Element(table => ComposeCategorisedServices(table, section));
            else
                column.Item().PaddingTop(0).Element(table => ComposeNumberedServices(table, section));
        });

    private static void ComposeNumberedServices(IContainer container, QuotationServiceSection section) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(7);
                columns.RelativeColumn(37);
                columns.RelativeColumn(56);
            });
            table.Header(header =>
            {
                HeaderCell(header.Cell(), "#", center: true);
                HeaderCell(header.Cell(), section.ServiceColumn);
                HeaderCell(header.Cell(), section.ScopeColumn);
            });

            var number = 0;
            foreach (var line in section.Groups.SelectMany(group => group.Lines))
            {
                var alternate = number % 2 == 1;
                number++;
                BodyCell(table.Cell(), alternate).AlignCenter().Text(QuotationPdfTemplate.Format(number)).Bold().FontSize(9).FontColor(Blue);
                BodyCell(table.Cell(), alternate).Text(line.Service).Bold().FontSize(9).FontColor(Navy);
                BodyCell(table.Cell(), alternate).Text(line.Scope).FontSize(9);
            }
        });

    private static void ComposeCategorisedServices(IContainer container, QuotationServiceSection section) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(19);
                columns.RelativeColumn(38);
                columns.RelativeColumn(43);
            });
            table.Header(header =>
            {
                HeaderCell(header.Cell(), "Category");
                HeaderCell(header.Cell(), section.ServiceColumn);
                HeaderCell(header.Cell(), section.ScopeColumn);
            });

            var colors = new[] { Navy, Blue, Green, DarkGreen };
            var groupIndex = 0;
            foreach (var group in section.Groups.Where(group => group.Lines.Count > 0))
            {
                var color = colors[groupIndex++ % colors.Length];
                var heading = group.Heading is null
                    ? string.Empty
                    : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(group.Heading.ToLowerInvariant());
                table.Cell().RowSpan((uint)group.Lines.Count).Background(color).BorderBottom(2).BorderColor(White)
                    .Padding(9).AlignMiddle().Text(heading).Bold().FontSize(10).FontColor(White);
                for (var index = 0; index < group.Lines.Count; index++)
                {
                    var line = group.Lines[index];
                    var alternate = index % 2 == 0;
                    BodyCell(table.Cell(), alternate).Text(line.Service).Bold().FontSize(9).FontColor(Navy);
                    BodyCell(table.Cell(), alternate).Text(line.Scope).FontSize(9);
                }
            }
        });

    private static void ComposeScope(IContainer container, QuotationPdfModel model, IReadOnlyList<Section> sections) =>
        container.Column(column =>
        {
            column.Spacing(10);
            SectionTitle(column, "scope", NumberOf(sections, "scope"), "Scope of Work");
            column.Item().PaddingTop(6).Column(list =>
            {
                list.Spacing(6);
                for (var index = 0; index < model.Items.Count; index++)
                {
                    var number = index + 1;
                    var item = model.Items[index];
                    list.Item().Row(row =>
                    {
                        row.ConstantItem(30).Width(22).Height(22).Background(number % 2 == 1 ? Navy : Green).AlignCenter().AlignMiddle()
                            .Text(QuotationPdfTemplate.Format(number)).Bold().FontSize(9.5f).FontColor(White);
                        row.RelativeItem().AlignMiddle().Text(item.Type).FontSize(11).Bold().FontColor(Navy);
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
                    BodyCell(table.Cell(), alternate, 6).AlignCenter().Text(item.PrNumber ?? "—");
                    BodyCell(table.Cell(), alternate, 6).Text(item.Description);
                    BodyCell(table.Cell(), alternate, 6).AlignCenter().Text(QuotationPdfTemplate.Format(item.Quantity));
                    BodyCell(table.Cell(), alternate, 6).AlignCenter().Text(QuotationDefaults.UnitOfMeasure);
                }
            });

            if (model.ScopeOfWork.Count > 0)
                column.Item().PaddingTop(10).Column(points =>
                {
                    points.Spacing(4);
                    points.Item().Text("The visit covers").Bold().FontSize(11).FontColor(Navy);
                    foreach (var point in model.ScopeOfWork) Bullet(points, point);
                });
        });

    private static readonly (string Title, string Body)[] MethodologySteps =
    [
        ("Planning & Document Review", "Review the scope, drawings, previous reports and client requirements, then agree the inspection plan and schedule."),
        ("Mobilization & Safety", "Mobilize personnel and equipment, complete the site safety induction and confirm permits and access."),
        ("Field Inspection & Measurement", "Carry out the inspection and testing in line with the approved procedures, recording all readings and observations."),
        ("Evaluation & Analysis", "Process the data, compare results against acceptance criteria and identify defects, trends and risks."),
        ("Reporting & Recommendations", "Issue a clear technical report with findings, photographs and recommended actions, and review it with your team.")
    ];

    private static void ComposeMethodology(IContainer container, IReadOnlyList<Section> sections) =>
        container.Column(column =>
        {
            column.Spacing(8);
            SectionTitle(column, "method", NumberOf(sections, "method"), "Technical Methodology");
            column.Item().PaddingTop(8).PaddingBottom(6).Text(
                "Our work follows a controlled five-step process, from planning to final report, so that results are traceable and repeatable.");
            for (var index = 0; index < MethodologySteps.Length; index++)
            {
                var (title, body) = MethodologySteps[index];
                var color = index % 2 == 0 ? Navy : Green;
                column.Item().Row(row =>
                {
                    row.ConstantItem(48).Height(54).Background(color).AlignCenter().AlignMiddle()
                        .Text(QuotationPdfTemplate.Format(index + 1)).Bold().FontSize(16).FontColor(White);
                    row.RelativeItem().PaddingLeft(14).AlignMiddle().Column(step =>
                    {
                        step.Item().Text(title).Bold().FontSize(13).FontColor(color);
                        step.Item().Text(body).FontSize(9.5f);
                    });
                });
            }

            column.Item().PaddingTop(10).Row(row =>
            {
                row.Spacing(14);
                row.RelativeItem().BorderTop(3).BorderColor(Blue).Background(BandBlue).Padding(12).Column(box =>
                {
                    box.Item().PaddingBottom(4).Text("STANDARDS & REFERENCES").Bold().FontSize(8.5f).LetterSpacing(0.25f).FontColor(Navy);
                    box.Item().Text("Applicable codes, standards and client procedures (for example API, ASME or ISO) as agreed for each item in the scope of work.")
                        .FontSize(9.5f);
                });
                row.RelativeItem().BorderTop(3).BorderColor(Green).Background(BandGreen).Padding(12).Column(box =>
                {
                    box.Item().PaddingBottom(4).Text("PERSONNEL & EQUIPMENT").Bold().FontSize(8.5f).LetterSpacing(0.25f).FontColor(DarkGreen);
                    box.Item().Text("Qualified inspectors using calibrated instruments, with calibration certificates available on request.")
                        .FontSize(9.5f);
                });
            });
        });

    private static void ComposePricing(IContainer container, QuotationPdfModel model, IReadOnlyList<Section> sections) =>
        container.Column(column =>
        {
            column.Spacing(12);
            SectionTitle(column, "pricing", NumberOf(sections, "pricing"), "Cost and Pricing");
            column.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(6);
                    columns.RelativeColumn(37);
                    columns.RelativeColumn(13);
                    columns.RelativeColumn(9);
                    columns.RelativeColumn(17);
                    columns.RelativeColumn(18);
                });
                table.Header(header =>
                {
                    HeaderCell(header.Cell(), "#", center: true);
                    HeaderCell(header.Cell(), "Description of Service");
                    HeaderCell(header.Cell(), "Unit", center: true);
                    HeaderCell(header.Cell(), "Qty", center: true);
                    HeaderCell(header.Cell(), "Unit Price", center: true);
                    HeaderCell(header.Cell(), "Amount", center: true);
                });
                for (var index = 0; index < model.Items.Count; index++)
                {
                    var item = model.Items[index];
                    var alternate = index % 2 == 1;
                    BodyCell(table.Cell(), alternate, 7).AlignCenter().Text(QuotationPdfTemplate.Format(index + 1)).Bold().FontSize(9).FontColor(Blue);
                    BodyCell(table.Cell(), alternate, 7).Text(item.Description);
                    BodyCell(table.Cell(), alternate, 7).AlignCenter().Text(item.PricingUnit).Bold().FontSize(9);
                    BodyCell(table.Cell(), alternate, 7).AlignCenter().Text(QuotationPdfTemplate.Format(item.Quantity)).Bold().FontSize(9);
                    BodyCell(table.Cell(), alternate, 7).AlignCenter().Text(QuotationPdfTemplate.FormatMoney(item.UnitPricePiastres)).Bold().FontSize(9);
                    BodyCell(table.Cell(), alternate, 7).AlignCenter().Text(QuotationPdfTemplate.FormatMoney(item.TotalPiastres)).Bold().FontSize(9);
                }
            });

            var notes = model.Notes.Append(QuotationDefaults.ValidityNote(model.ValidForDays)).ToList();
            var totalLength = notes.Sum(note => note.Length);

            // A few short notes sit beside the totals as in the design; a long list takes the full width underneath so it can run onto the next page.
            if (totalLength <= 1100)
                column.Item().Row(row =>
                {
                    row.RelativeItem(11).Element(remarks => ComposeRemarks(remarks, notes));
                    row.ConstantItem(14);
                    row.RelativeItem(9).Element(totals => ComposeTotals(totals, model));
                });
            else
            {
                column.Item().AlignRight().Width(250).Element(totals => ComposeTotals(totals, model));
                column.Item().Element(remarks => ComposeRemarks(remarks, notes));
            }
        });

    private static void ComposeRemarks(IContainer container, IReadOnlyList<string> notes) =>
        container.Border(0.8f).BorderColor(Line).Padding(10).Column(column =>
        {
            column.Spacing(3);
            column.Item().PaddingBottom(2).Text("REMARKS").Bold().FontSize(8.5f).LetterSpacing(0.3f).FontColor(Navy);
            foreach (var note in notes) Bullet(column, QuotationPdfTemplate.EmphasizeLeadIn(note), 8.5f);
        });

    // The discount is worked out by the same function as in the quotation, so the PDF and the screen show the same figures.
    private static void ComposeTotals(IContainer container, QuotationPdfModel model) =>
        container.Column(column =>
        {
            var subtotal = model.Items.Aggregate(0L, (sum, item) => checked(sum + (item.TotalPiastres ?? 0)));
            var discount = Quote.DiscountOf(subtotal, model.DiscountPercent);
            var vat = model.VatPercent > 0m ? Quote.VatOf(subtotal - discount, model.VatPercent) : 0L;

            void Row(string label, string value) =>
                column.Item().BorderBottom(0.5f).BorderColor(Line).PaddingVertical(11).Row(row =>
                {
                    row.RelativeItem().PaddingLeft(10).Text(label).Bold().FontSize(10).FontColor(Muted);
                    row.ConstantItem(112).AlignCenter().Text(value).Bold().FontSize(10).FontColor(Navy);
                });

            if (model.DiscountPercent > 0m || model.VatPercent > 0m)
                Row("Subtotal", QuotationPdfTemplate.FormatMoney(subtotal));
            if (model.DiscountPercent > 0m)
                Row($"Discount ({model.DiscountPercent.ToString("0.##", CultureInfo.InvariantCulture)}%)", "- " + QuotationPdfTemplate.FormatMoney(discount));
            if (model.VatPercent > 0m)
                Row($"VAT ({model.VatPercent.ToString("0.##", CultureInfo.InvariantCulture)}%)", QuotationPdfTemplate.FormatMoney(vat));

            column.Item().Background(Green).PaddingVertical(15).Row(row =>
            {
                row.RelativeItem().PaddingLeft(10).Text("TOTAL").Bold().FontSize(11).LetterSpacing(0.2f).FontColor(White);
                row.ConstantItem(112).AlignCenter().Text(QuotationPdfTemplate.FormatMoney(subtotal - discount + vat)).Bold().FontSize(10.5f).FontColor(White);
            });
        });

    private static void ComposeTerms(IContainer container, QuotationPdfModel model, QuotationCompanyProfile company, IReadOnlyList<Section> sections)
    {
        var number = NumberOf(sections, "terms");
        container.Column(column =>
        {
            column.Spacing(9);
            SectionTitle(column, "terms", number, "Terms and Conditions");
            for (var index = 0; index < company.Terms.Count; index++)
            {
                var term = company.Terms[index];
                column.Item().PaddingTop(index == 0 ? 6 : 2).Column(block =>
                {
                    block.Spacing(4);
                    SubBand(block, $"{number}.{index + 1}", WithoutNumber(term.Title), navy: index % 2 == 0);
                    block.Item().PaddingTop(2).Column(points =>
                    {
                        points.Spacing(4);
                        foreach (var point in term.Points)
                            Bullet(points, point.Replace("{days}", model.ValidForDays.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
                    });
                    if (term.ShowBankAccounts && company.BankAccounts.Count > 0)
                        block.Item().PaddingTop(2).Element(accounts => ComposeBankAccounts(accounts, company.BankAccounts));
                });
            }

            column.Item().PaddingTop(12).Column(thanks =>
            {
                thanks.Item().Height(3).Background(Blue);
                thanks.Item().Background(DeepGreen).PaddingVertical(16).Column(box =>
                {
                    box.Item().AlignCenter().Text("THANK YOU FOR YOUR TRUST").Bold().FontSize(14).LetterSpacing(0.28f).FontColor(White);
                    box.Item().PaddingTop(3).AlignCenter().Text("We look forward to working with you.").FontSize(9.5f).FontColor(White);
                });
            });
        });
    }

    private static void ComposeBankAccounts(IContainer container, IReadOnlyList<QuotationBankAccount> accounts) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(14);
                columns.RelativeColumn(15);
                columns.RelativeColumn(9);
                columns.RelativeColumn(15);
                columns.RelativeColumn(33);
                columns.RelativeColumn(15);
            });
            table.Header(header =>
            {
                foreach (var title in new[] { "Beneficiary Name", "Bank Name", "Currency", "Account Number", "IBAN", "SWIFT Code" })
                    header.Cell().Background(Navy).PaddingVertical(7).PaddingHorizontal(5).AlignCenter()
                        .Text(title).Bold().FontSize(8).FontColor(White);
            });
            for (var index = 0; index < accounts.Count; index++)
            {
                var account = accounts[index];
                var fill = index % 2 == 0 ? BandGreen : White;
                table.Cell().Background(fill).PaddingVertical(6).PaddingHorizontal(5).AlignCenter().Text(account.Beneficiary).Bold().FontSize(8).FontColor(Navy);
                foreach (var value in new[] { account.Bank, account.Currency, account.AccountNumber, account.Iban, account.Swift })
                    table.Cell().Background(fill).PaddingVertical(6).PaddingHorizontal(5).AlignCenter().AlignMiddle().Text(value).FontSize(8);
            }
        });
}
