using QuestPDF.Infrastructure;

namespace Inspecta.Infrastructure.Pdf;

/// <summary>Shared look of every generated document, so new templates stay consistent.</summary>
public static class PdfTheme
{
    public static readonly Color Navy = Color.FromHex("#13285D");
    public static readonly Color Green = Color.FromHex("#3FA14D");
    public static readonly Color DarkGreen = Color.FromHex("#237936");
    public static readonly Color Body = Color.FromHex("#1B2433");
    public static readonly Color Muted = Color.FromHex("#5B6B85");
    public static readonly Color Band = Color.FromHex("#EEF3FA");
    public static readonly Color RowAlt = Color.FromHex("#F4F7FC");
    public static readonly Color Line = Color.FromHex("#DBE3EF");
    public static readonly Color White = Color.FromHex("#FFFFFF");

    public static Color Primary => Navy;

    public const float PageMargin = 40;
    public const float BodyFontSize = 10;

    /// <summary>Font used by all templates: Inter when its files are embedded (see <see cref="PdfFonts"/>), otherwise QuestPDF's built-in Lato.</summary>
    public static string FontFamily => PdfFonts.Family;
}
