using System.Net;
using System.Text;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Inspecta.Api.Controllers;

/// <summary>
/// The public check behind the QR code of a certificate. No sign-in is needed; it says whether the certificate was
/// issued by us, shows what is printed on it and lets the holder download the certificate itself. Limited per address.
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicy)]
[ApiExplorerSettings(IgnoreApi = true)] // the QR opens it; it is not part of the app's workflow
public sealed class VerificationController(ClientCertificateService certificates) : ControllerBase
{
    public const string RateLimitPolicy = "verify";

    // The host the request was opened on (no port). Only Request.Host: the proxy keeps the original Host, and X-Forwarded-Host is never read
    // (the forwarded-headers middleware is configured for X-Forwarded-For and -Proto only), so a caller cannot pick which company's rules apply.
    private string RequestHost => Request.Host.Host;

    /// <summary>The page a person sees after scanning the QR code.</summary>
    [HttpGet("/verify/{code}")]
    [Produces("text/html")]
    public async Task<ContentResult> Page(string code, CancellationToken cancellationToken)
    {
        var result = await certificates.VerifyAsync(code, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken, RequestHost);
        Response.Headers.CacheControl = "no-store";
        // A page of text with one inline style: no scripts, no framing, nothing fetched from elsewhere.
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; img-src data:; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
        return new ContentResult
        {
            Content = Render(result),
            ContentType = "text/html; charset=utf-8",
            StatusCode = result.Valid ? StatusCodes.Status200OK : StatusCodes.Status404NotFound
        };
    }

    /// <summary>The certificate itself, as it was issued, opened from the page's download button.</summary>
    [HttpGet("/verify/{code}/pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Pdf(string code, CancellationToken cancellationToken)
    {
        var file = await certificates.GetPdfByCodeAsync(code, cancellationToken, RequestHost);
        if (file is null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        // Inline, so a phone opens it in the browser; the name is used when it is saved.
        Response.Headers.ContentDisposition = $"inline; filename=\"{file.FileName}\"";
        return File(file.Content, "application/pdf");
    }

    /// <summary>The same check as JSON, for an app.</summary>
    [HttpGet("/api/v1/verify/{code}")]
    [ProducesResponseType<CertificateVerificationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<CertificateVerificationResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CertificateVerificationResponse>> Json(string code, CancellationToken cancellationToken)
    {
        var result = await certificates.VerifyAsync(code, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken, RequestHost);
        Response.Headers.CacheControl = "no-store";
        return result.Valid ? Ok(result) : NotFound(result);
    }

    private static string Render(CertificateVerificationResponse result)
    {
        string H(string? value) => WebUtility.HtmlEncode(value ?? "—");
        string Row(string label, string? value) => $"<tr><th>{label}</th><td>{H(value)}</td></tr>";

        var (colour, headline) = result.Status switch
        {
            "valid" => ("#1f8a3b", "Genuine certificate"),
            "expired" => ("#b7791f", "Genuine certificate - its next examination date has passed"),
            _ => ("#c53030", "Certificate not found")
        };
        var body = new StringBuilder();
        // The company that issued the certificate is named first, large, so whoever scans the QR code sees whose certificate it is.
        if (result.Valid && !string.IsNullOrWhiteSpace(result.IssuedBy)) body.Append($"<div class=\"brand\">{H(result.IssuedBy)}</div>");
        body.Append($"<h1 style=\"color:{colour}\">{headline}</h1>");
        if (result.Valid)
        {
            body.Append("<table>");
            body.Append(Row("Certificate number", result.CertificateNumber));
            body.Append(Row("Inspected item", result.ItemName));
            if (result.Result is { } verdict) body.Append(Row("Result", verdict.Label()));
            body.Append(Row("Client", result.ClientName));
            body.Append(Row("Examination date", result.ExaminationDate?.ToString("dd/MM/yyyy")));
            body.Append(Row("Next examination date", result.NextExaminationDate?.ToString("dd/MM/yyyy")));
            body.Append(Row("Issued", result.IssuedAt?.UtcDateTime.ToString("dd/MM/yyyy")));
            body.Append(Row("Issued by", result.IssuedBy));
            body.Append("</table>");
            if (result.PdfUrl is { } pdfUrl)
                body.Append($"<a class=\"button\" href=\"{WebUtility.HtmlEncode(pdfUrl)}\">Download certificate (PDF)</a>");
        }
        else
        {
            body.Append("<p>This code does not match a certificate we issued. Check that the whole QR code was scanned.</p>");
        }

        return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
               "<title>Certificate verification</title><style>" +
               "body{font-family:system-ui,Segoe UI,Arial,sans-serif;background:#f4f6fa;margin:0;padding:24px;color:#1b2433}" +
               ".card{max-width:520px;margin:24px auto;background:#fff;border:1px solid #dbe3ef;border-radius:10px;padding:22px}" +
               ".brand{font-size:18px;font-weight:700;color:#184896;margin:0 0 4px}h1{font-size:20px;margin:0 0 14px}table{width:100%;border-collapse:collapse}" +
               "th{text-align:left;color:#5b6b85;font-weight:600;padding:6px 8px 6px 0;width:42%}td{padding:6px 0}" +
               "tr+tr{border-top:1px solid #edf1f7}.foot{margin-top:16px;color:#5b6b85;font-size:12px}" +
               ".button{display:block;margin-top:18px;padding:12px;border-radius:8px;background:#2b66a6;color:#fff;text-align:center;font-weight:600;text-decoration:none}" +
               "</style></head><body><div class=\"card\">" + body +
               // The company that issued it; a code that matches nothing names no company.
               (result.Valid ? $"<div class=\"foot\">{H(result.IssuedBy)} - Report of Thorough Examination</div>" : string.Empty) +
               "</div></body></html>";
    }
}
