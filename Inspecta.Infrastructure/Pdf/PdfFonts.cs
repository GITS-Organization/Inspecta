using System.Reflection;
using QuestPDF.Drawing;

namespace Inspecta.Infrastructure.Pdf;

/// <summary>
/// Registers the fonts embedded under <c>Pdf/Fonts</c> (any <c>.ttf</c> file). When Inter files are there the documents
/// use Inter, as the reference design does; without them QuestPDF's built-in font (Lato) is used.
/// </summary>
public static class PdfFonts
{
    private const string PreferredFamily = "Inter";
    private const string FallbackFamily = "Lato";

    public static string Family { get; private set; } = FallbackFamily;

    public static void Register()
    {
        var assembly = typeof(PdfFonts).Assembly;
        var registered = false;
        foreach (var name in assembly.GetManifestResourceNames()
                     .Where(resource => resource.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) continue;
            FontManager.RegisterFont(stream);
            registered |= name.Contains(PreferredFamily, StringComparison.OrdinalIgnoreCase);
        }

        Family = registered ? PreferredFamily : FallbackFamily;
    }
}
