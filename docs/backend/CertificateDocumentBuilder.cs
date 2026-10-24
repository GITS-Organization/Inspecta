// Sample only: not compiled or run against QuestPDF. Treat it as a starting point.
// Needs: QuestPDF (2024.x or newer, for auto-placed table cells), QRCoder (QR image).
//
// Usage:
//   var pdf = CertificateDocumentBuilder.Create()
//       .With(data)
//       .WithBranding(branding)
//       .WithQrCode(QrPng.From(data.VerificationUrl))
//       .HideIf(data.Note is null, CertificateSection.Note)
//       .Hide(CertificateSection.GeneralNotes)
//       .GeneratePdf();

using System.Globalization;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Inspecta.Certificates;

// ───────────────────────────── Data ─────────────────────────────

/// <summary>Sections that can be switched off with Hide / HideIf.</summary>
public enum CertificateSection
{
    Header,
    ClientTable,
    ItemInformation,
    Questions,
    DefectDetails,
    Note,
    GeneralNotes,
    Signatures,
    QrCode,   // only the QR cell inside Signatures
    Footer,
}

public sealed record CertificateData
{
    public required string ClientName { get; init; }
    public required string Location { get; init; }
    public string ClientRepresentative { get; init; } = "N/A";
    public required string CertificateNumber { get; init; }
    public required DateOnly CertificateDate { get; init; }
    public required DateOnly ExaminationDate { get; init; }
    public required DateOnly NextExaminationDate { get; init; }
    public string Standard { get; init; } = "N/A";
    public string TestType { get; init; } = "Visual & function test";

    public required string InspectedItem { get; init; }
    public string Manufacturer { get; init; } = "N/A";
    public string ModelAndYear { get; init; } = "N/A";
    public string MaxWorkingRate { get; init; } = "N/A";
    public string SerialNumber { get; init; } = "N/A";
    public string FunctionCheck { get; init; } = "N/A";
    public decimal? AngleReadingDegrees { get; init; }   // only set when the equipment has an angle
    public string OwnerId { get; init; } = "N/A";
    public string Ndt { get; init; } = "N/A";
    public string Conclusion { get; init; } = "N/A";

    // null = not answered (both boxes stay empty)
    public bool? FirstAfterInstallation { get; init; }
    public bool? InstalledCorrectly { get; init; }
    public bool? Within6Months { get; init; }
    public bool? Within12Months { get; init; }
    public bool? PerExaminationScheme { get; init; }
    public bool? AfterExceptionalCircumstances { get; init; }
    public bool? ExistingOrImminentDanger { get; init; }

    public string DefectDescription { get; init; } = "NONE";
    public string PotentialDangerBy { get; init; } = "N/A";
    public string RepairsRequired { get; init; } = "N/A";
    public string TestsCarriedOut { get; init; } = "NONE";
    public string? Note { get; init; }

    public required string PreparedBy { get; init; }
    public string PreparedByQualifications { get; init; } = "";
    public required string SignedBy { get; init; }
    public string VerificationUrl { get; init; } = "";

    public IReadOnlyList<string> GeneralNotes { get; init; } =
    [
        "This Certificate becomes invalid if any Repair/modification is done that affects strength or stability.",
        "Any holder of this document is advised that the information contained hereon is limited to the accessible portions of the item examined only.",
        "Equipment has been correctly installed and fit for the purpose",
        "This Certificate should be signed, stamped and carries unique Integrity for inspection and Engineering Solutions certificate number",
    ];
}

public sealed record CertificateBranding
{
    public required byte[] Logo { get; init; }
    public byte[]? Accreditations { get; init; }
    public string CompanyName { get; init; } = "Integrity International Inspection.";
    public string[] AddressLines { get; init; } = [];
    public string[] FooterLines { get; init; } = [];
    public string LatinFont { get; init; } = "Inter";
    public string ArabicFont { get; init; } = "Noto Naskh Arabic";
}

// ───────────────────────────── Builder ─────────────────────────────

public sealed class CertificateDocumentBuilder
{
    private readonly HashSet<CertificateSection> _hidden = [];
    private CertificateData? _data;
    private CertificateBranding? _branding;
    private byte[]? _qrPng;

    private CertificateDocumentBuilder() { }

    public static CertificateDocumentBuilder Create() => new();

    public CertificateDocumentBuilder With(CertificateData data) { _data = data; return this; }

    public CertificateDocumentBuilder WithBranding(CertificateBranding branding) { _branding = branding; return this; }

    public CertificateDocumentBuilder WithQrCode(byte[] png) { _qrPng = png; return this; }

    public CertificateDocumentBuilder Hide(params CertificateSection[] sections)
    {
        foreach (var s in sections) _hidden.Add(s);
        return this;
    }

    public CertificateDocumentBuilder HideIf(bool condition, params CertificateSection[] sections)
        => condition ? Hide(sections) : this;

    public CertificateDocumentBuilder Show(params CertificateSection[] sections)
    {
        foreach (var s in sections) _hidden.Remove(s);
        return this;
    }

    public IDocument Build()
    {
        if (_data is null) throw new InvalidOperationException("Call With(data) first.");
        if (_branding is null) throw new InvalidOperationException("Call WithBranding(...) first.");
        return new CertificateDocument(_data, _branding, _qrPng, _hidden.ToHashSet());
    }

    public byte[] GeneratePdf() => Build().GeneratePdf();

    public void GeneratePdf(string path) => Build().GeneratePdf(path);
}

// ───────────────────────────── Rendering ─────────────────────────────

internal sealed class CertificateDocument(
    CertificateData d,
    CertificateBranding b,
    byte[]? qrPng,
    HashSet<CertificateSection> hidden) : IDocument
{
    private const float Line = 0.6f;
    private static readonly string Red = "#E10600";

    private bool Visible(CertificateSection s) => !hidden.Contains(s);

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Report of thorough examination {d.CertificateNumber}",
        Author = b.CompanyName,
    };

    public void Compose(IDocumentContainer container) =>
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(24);
            page.DefaultTextStyle(t => t
                .FontFamily(b.LatinFont).FontSize(8)
                .Fallback(f => f.FontFamily(b.ArabicFont)));

            page.Content().Border(0.8f).Padding(14).Column(col =>
            {
                col.Spacing(10);

                if (Visible(CertificateSection.Header)) col.Item().Element(Header);
                if (Visible(CertificateSection.ClientTable)) col.Item().Element(ClientTable);
                if (Visible(CertificateSection.ItemInformation)) col.Item().Element(ItemInformation);
                if (Visible(CertificateSection.Questions)) col.Item().Element(Questions);
                if (Visible(CertificateSection.DefectDetails)) col.Item().Element(DefectDetails);
                if (Visible(CertificateSection.Note)) col.Item().Element(Note);
                if (Visible(CertificateSection.GeneralNotes)) col.Item().Element(GeneralNotes);
                if (Visible(CertificateSection.Signatures)) col.Item().Element(Signatures);
            });

            if (Visible(CertificateSection.Footer))
                page.Footer().PaddingTop(6).Element(Footer);
        });

    // ── Sections ──

    private void Header(IContainer c) => c.Column(col =>
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Column(info =>
            {
                info.Item().Text(b.CompanyName).Bold();
                foreach (var line in b.AddressLines) info.Item().Text(line);
            });
            row.ConstantItem(70).Height(64).Image(b.Logo).FitArea();
        });
        col.Item().PaddingTop(6).AlignCenter()
            .Text("REPORT OF THOROUGH EXAMINATION")
            .FontSize(13).Bold().FontColor(Red);
    });

    private void ClientTable(IContainer c) => Framed(c).Table(t =>
    {
        t.ColumnsDefinition(cols => { for (var i = 0; i < 12; i++) cols.RelativeColumn(); });

        t.Cell().ColumnSpan(4).Element(Cells.Head).Text("Client and Location of thorough examination");
        t.Cell().ColumnSpan(2).Element(Cells.Head).Text("Client representative");
        t.Cell().ColumnSpan(3).Element(Cells.Head).Text("Certificate number");
        t.Cell().ColumnSpan(3).Element(Cells.Head).Text("Certificate date");

        t.Cell().ColumnSpan(4).Element(Cells.Center).Column(x =>
        {
            x.Item().AlignCenter().Text(d.ClientName).Bold();
            x.Item().AlignCenter().Text(d.Location).FontSize(7);
        });
        t.Cell().ColumnSpan(2).Element(Cells.Center).Text(d.ClientRepresentative).Bold();
        t.Cell().ColumnSpan(3).Element(Cells.Center).Text(d.CertificateNumber).Bold();
        t.Cell().ColumnSpan(3).Element(Cells.Center).Text(Date(d.CertificateDate)).Bold();

        t.Cell().ColumnSpan(2).Element(Cells.Head).Text("Through examination date");
        t.Cell().ColumnSpan(2).Element(Cells.Head).Text("Next examination date");
        t.Cell().ColumnSpan(5).Element(Cells.Head).Text("Standard of inspection");
        t.Cell().ColumnSpan(3).Element(Cells.Head).Text("Test type");

        t.Cell().ColumnSpan(2).Element(Cells.Center).Text(Date(d.ExaminationDate)).Bold();
        t.Cell().ColumnSpan(2).Element(Cells.Center).Text(Date(d.NextExaminationDate)).Bold();
        t.Cell().ColumnSpan(5).Element(Cells.Center).Text(d.Standard);
        t.Cell().ColumnSpan(3).Element(Cells.Center).Text(d.TestType).Bold();
    });

    private void ItemInformation(IContainer c) => Framed(c).Column(col =>
    {
        col.Item().Element(Cells.Head).Text("Item information / Test results.");
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(2.2f); cols.RelativeColumn(2.6f);
                cols.RelativeColumn(2.2f); cols.RelativeColumn(2.0f);
            });

            Pair(t, "Inspected Item:", d.InspectedItem, "Manufacturer:", d.Manufacturer);
            Pair(t, "Model/ year of manufacturing:", d.ModelAndYear, "MAX. working rate:", d.MaxWorkingRate);
            Pair(t, "Serial number / Chassis number:", d.SerialNumber, "Function Check:", d.FunctionCheck);
            Pair(t, "Owner ID:", d.OwnerId, "NDT:", d.Ndt);

            if (d.AngleReadingDegrees is { } angle)
                Pair(t, "Angle reading:", $"{angle.ToString("0.##", CultureInfo.InvariantCulture)}°", "", "");

            t.Cell().Element(Cells.HeadLeft).Text("Conclusion / remarks:");
            t.Cell().ColumnSpan(3).Element(Cells.Head).Text(d.Conclusion);
        });
    });

    private void Questions(IContainer c) => Framed(c).Table(t =>
    {
        t.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.ConstantColumn(90); });

        Question(t, "Is this the first examination after installation or assembly at a new site or location?", d.FirstAfterInstallation);
        Question(t, "If the answer to the above question is YES has the equipment been installed correctly?", d.InstalledCorrectly);
        Question(t, "Was the examination carried out within an interval of 6 months?", d.Within6Months);
        Question(t, "Was the examination carried out within an interval of 12 months?", d.Within12Months);
        Question(t, "Was the examination carried out in accordance with an examination scheme?", d.PerExaminationScheme);
        Question(t, "Was the examination carried out after the occurrence of exceptional circumstances?", d.AfterExceptionalCircumstances);
        Question(t, "Is the above an existing or imminent danger to persons? *Note-This is a reportable defect", d.ExistingOrImminentDanger);
    });

    private void DefectDetails(IContainer c) => Framed(c).Column(col =>
    {
        TextRow(col, $"Identification of any part found to have a defect which is or could become a danger to persons and a description of the defect: {d.DefectDescription}");
        TextRow(col, $"Is the above a defect which is not yet but could become a danger to persons (if YES state the date by when): {d.PotentialDangerBy}");
        TextRow(col, $"Particulars of any repair, renewal or alteration required to remedy the defect identified above: {d.RepairsRequired}");
        TextRow(col, $"Particulars of any tests carried out as part of the examination: {d.TestsCarriedOut}");
    });

    private void Note(IContainer c) => Framed(c).Element(Cells.Plain).MinHeight(50).Text($"Note: {d.Note}");

    private void GeneralNotes(IContainer c) => Framed(c).Table(t =>
    {
        t.ColumnsDefinition(cols => { cols.RelativeColumn(1.4f); cols.RelativeColumn(5f); });

        t.Cell().Element(Cells.Center).Text("General Notes and Recommendations");
        t.Cell().Element(Cells.Plain).Column(col =>
        {
            col.Spacing(2);
            foreach (var n in d.GeneralNotes)
                col.Item().Row(r =>
                {
                    r.ConstantItem(10).Text("•");
                    r.RelativeItem().Text(n).FontSize(7);
                });
        });
    });

    private void Signatures(IContainer c)
    {
        var showQr = Visible(CertificateSection.QrCode) && qrPng is not null;

        Framed(c).Table(t =>
        {
            t.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(2.4f); cols.RelativeColumn(2.4f); cols.RelativeColumn(2.4f);
                if (showQr) cols.RelativeColumn(1.5f);
            });

            t.Cell().Element(Cells.Center).Text("Name & Qualifications of person making this report");
            t.Cell().Element(Cells.Center).Text("Name of person signing or authenticating this report on behalf of the author");
            t.Cell().Element(Cells.Center).Text($"For {b.CompanyName.TrimEnd('.')}");
            if (showQr) t.Cell().Element(Cells.Center).Text("QR Code");

            t.Cell().MinHeight(70).Element(Cells.Center).Column(x =>
            {
                x.Item().AlignCenter().Text(d.PreparedBy).Bold();
                x.Item().AlignCenter().Text(d.PreparedByQualifications).FontSize(7);
            });
            t.Cell().MinHeight(70).Element(Cells.Center).Text(""); // drawn signature goes here
            t.Cell().MinHeight(70).Element(Cells.Center).Text(d.SignedBy).Bold();
            if (showQr) t.Cell().MinHeight(70).Element(Cells.Center).Padding(4).Image(qrPng!).FitArea();
        });
    }

    private void Footer(IContainer c) => c.Row(row =>
    {
        row.RelativeItem().Column(col =>
        {
            foreach (var line in b.FooterLines) col.Item().Text(line).FontSize(6);
        });
        if (b.Accreditations is not null)
            row.ConstantItem(138).Height(34).Image(b.Accreditations).FitArea();
    });

    // ── Small helpers ──

    /// Section frame: top + left here, right + bottom on every cell, so lines never double up.
    private static IContainer Framed(IContainer c) => c.BorderTop(Line).BorderLeft(Line);

    private static string Date(DateOnly v) => v.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static void Pair(TableDescriptor t, string l1, string v1, string l2, string v2)
    {
        t.Cell().Element(Cells.HeadLeft).Text(l1);
        t.Cell().Element(Cells.Plain).Text(v1);
        t.Cell().Element(Cells.HeadLeft).Text(l2);
        t.Cell().Element(Cells.Center).Text(v2);
    }

    private static void Question(TableDescriptor t, string text, bool? answer)
    {
        t.Cell().Element(Cells.Plain).Text(text);
        t.Cell().Element(Cells.Center).Row(r =>
        {
            r.Spacing(4);
            r.AutoItem().Text("YES");
            r.ConstantItem(12).Height(12).Border(Line).AlignCenter().AlignMiddle().Text(answer == true ? "X" : "");
            r.AutoItem().Text("NO");
            r.ConstantItem(12).Height(12).Border(Line).AlignCenter().AlignMiddle().Text(answer == false ? "X" : "");
        });
    }

    private static void TextRow(ColumnDescriptor col, string text) =>
        col.Item().Element(Cells.Plain).Text(text);

    private static class Cells
    {
        public static IContainer Head(IContainer c) => Base(c).Background(Colors.Grey.Lighten3).AlignCenter().AlignMiddle()
            .DefaultTextStyle(x => x.Bold());

        public static IContainer HeadLeft(IContainer c) => Base(c).Background(Colors.Grey.Lighten3).AlignMiddle()
            .DefaultTextStyle(x => x.Bold());

        public static IContainer Center(IContainer c) => Base(c).AlignCenter().AlignMiddle();

        public static IContainer Plain(IContainer c) => Base(c).AlignMiddle();

        private static IContainer Base(IContainer c) =>
            c.BorderRight(Line).BorderBottom(Line).PaddingVertical(3).PaddingHorizontal(5);
    }
}

// ───────────────────────────── QR helper ─────────────────────────────

public static class QrPng
{
    public static byte[] From(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(8);
    }
}

// ───────────────────────────── Startup (once) ─────────────────────────────
// QuestPDF.Settings.License = LicenseType.Community;   // check your company's eligibility
// FontManager.RegisterFont(File.OpenRead("Fonts/Inter-Regular.ttf"));
// FontManager.RegisterFont(File.OpenRead("Fonts/Inter-Bold.ttf"));
// FontManager.RegisterFont(File.OpenRead("Fonts/NotoNaskhArabic-Regular.ttf"));
// FontManager.RegisterFont(File.OpenRead("Fonts/NotoNaskhArabic-Bold.ttf"));
