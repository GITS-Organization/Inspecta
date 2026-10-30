using System.Globalization;
using System.Text;

namespace Inspecta.Infrastructure.Pdf;

/// <summary>Images embedded in the assembly and small SVG drawings shared by the templates.</summary>
public static class PdfAssets
{
    private static readonly Lazy<byte[]> QuotationCoverImage = new(() => Load("quotation-cover.png"));
    private static readonly Lazy<byte[]> LogoImage = new(() => Load("logo.png"));
    private static readonly Lazy<byte[]> AccreditationsImage = new(() => Load("accreditations.png"));

    public static byte[] QuotationCover => QuotationCoverImage.Value;
    public static byte[] Logo => LogoImage.Value;

    /// <summary>The EGAC and ISO marks printed at the foot of a certificate.</summary>
    public static byte[] Accreditations => AccreditationsImage.Value;

    private static byte[] Load(string fileName)
    {
        var assembly = typeof(PdfAssets).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>The faint blueprint grid behind every page: a line every 26 pt, alternating two shades.</summary>
    public static string GridSvg(float width, float height)
    {
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
        for (var index = 1; index * 26 < width; index++)
            svg.Append(CultureInfo.InvariantCulture, $"<rect x=\"{index * 26}\" y=\"0\" width=\"0.6\" height=\"{height}\" fill=\"{(index % 2 == 1 ? "#E6EDF7" : "#F2F6FB")}\"/>");
        for (var index = 1; index * 26 < height; index++)
            svg.Append(CultureInfo.InvariantCulture, $"<rect x=\"0\" y=\"{index * 26}\" width=\"{width}\" height=\"0.6\" fill=\"{(index % 2 == 1 ? "#E6EDF7" : "#F2F6FB")}\"/>");
        svg.Append("</svg>");
        return svg.ToString();
    }

    /// <summary>The diagonal navy and green safety stripe used under the header and at the end of the document.</summary>
    public static string StripeSvg(float width, float height, float stripeWidth)
    {
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
        svg.Append(CultureInfo.InvariantCulture, $"<defs><clipPath id=\"clip\"><rect width=\"{width}\" height=\"{height}\"/></clipPath></defs>");
        svg.Append(CultureInfo.InvariantCulture, $"<g clip-path=\"url(#clip)\"><rect width=\"{width}\" height=\"{height}\" fill=\"#13285D\"/>");
        for (var x = -height; x < width + height; x += stripeWidth * 2)
            svg.Append(CultureInfo.InvariantCulture, $"<polygon points=\"{x},{height} {x + stripeWidth},{height} {x + stripeWidth + height},0 {x + height},0\" fill=\"#3FA14D\"/>");
        svg.Append("</g></svg>");
        return svg.ToString();
    }

    /// <summary>A small green play triangle used as a list bullet.</summary>
    public const string TriangleBulletSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"8\" height=\"8\" viewBox=\"0 0 8 8\"><polygon points=\"1,0.5 7,4 1,7.5\" fill=\"#3FA14D\"/></svg>";
}
