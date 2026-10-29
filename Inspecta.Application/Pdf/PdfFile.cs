namespace Inspecta.Application.Pdf;

/// <summary>A generated PDF document ready to be returned, stored or attached to an email.</summary>
public sealed record PdfFile(byte[] Content, string FileName)
{
    public const string ContentType = "application/pdf";
}
