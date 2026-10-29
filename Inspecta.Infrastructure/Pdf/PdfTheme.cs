using QuestPDF.Infrastructure;

namespace Inspecta.Infrastructure.Pdf;

/// <summary>Shared look of every generated document, so new templates stay consistent.</summary>
public static class PdfTheme
{
    public static readonly Color Primary = Color.FromHex("#0B1A3B");
    public static readonly Color Muted = Color.FromHex("#5B6478");
    public static readonly Color Line = Color.FromHex("#D5DAE5");

    public const float PageMargin = 40;
    public const float BodyFontSize = 10;
}
