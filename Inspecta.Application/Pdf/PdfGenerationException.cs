namespace Inspecta.Application.Pdf;

public sealed class PdfGenerationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
