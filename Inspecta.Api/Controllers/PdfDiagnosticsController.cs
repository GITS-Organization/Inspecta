using Inspecta.Api.Authorization;
using Inspecta.Application.Pdf;
using Inspecta.Application.Pdf.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>Checks that PDF generation works in the running environment (native libraries, fonts, license).</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/v1/admin/pdf")]
public sealed class PdfDiagnosticsController(IPdfGenerator pdfGenerator) : ControllerBase
{
    /// <summary>Download a sample quotation PDF built from fixed data.</summary>
    [HttpGet("sample")]
    [Produces(PdfFile.ContentType)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Sample()
    {
        var file = pdfGenerator.Generate(SampleModel());
        return File(file.Content, PdfFile.ContentType, file.FileName);
    }

    private static QuotationPdfModel SampleModel() => new(
        "QT-2026-0142 rev01",
        "Delta Steel Co.",
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow.AddDays(14),
        "Thank you for your enquiry. Please find our offer below.",
        [
            new QuotationPdfItem("Mobile crane - Liebherr LTM 1100, 100 t", "13351021", 1, 1_200_000, "Per Visit"),
            new QuotationPdfItem("Air receiver - 3000 L", null, 2, 850_000, "Per Visit")
        ],
        ["Load and functional test", "Visual inspection", "Ultrasonic thickness measurements"],
        ["Pricing excludes VAT.", "Payment terms: 90 days from invoice date."]);
}
