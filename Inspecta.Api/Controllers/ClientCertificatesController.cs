using Inspecta.Api.Authorization;
using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Pdf;
using Inspecta.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inspecta.Api.Controllers;

/// <summary>The certificates issued for a client, kept exactly as they were issued.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.TeamOperations)]
[Route("api/v1/clients/{clientId:guid}/certificates")]
public sealed class ClientCertificatesController(ClientCertificateService certificates) : ControllerBase
{
    /// <summary>List the certificates issued for a client, newest first.</summary>
    /// <remarks>
    /// A certificate is filed here when the technical manager approves it. Each row has the certificate number, the item,
    /// the examination and next examination dates and a SHA-256 of the PDF. Open one with <c>/{id}/pdf</c>.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ClientCertificateResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ClientCertificateResponse>>> List(Guid clientId, CancellationToken cancellationToken) =>
        Ok(await certificates.ListAsync(clientId, cancellationToken));

    /// <summary>Download an issued certificate as PDF.</summary>
    [HttpGet("{certificateFileId:guid}/pdf")]
    [Produces(PdfFile.ContentType)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPdf(Guid clientId, Guid certificateFileId, CancellationToken cancellationToken)
    {
        var file = await certificates.GetPdfAsync(clientId, certificateFileId, cancellationToken);
        return File(file.Content, PdfFile.ContentType, file.FileName);
    }

    /// <summary>Download the QR code of an issued certificate as a PNG.</summary>
    /// <remarks>The QR printed on the certificate, as an image to send to the client. It opens the public check page with the PDF download (<c>qrUrl</c> of the list).</remarks>
    [HttpGet("{certificateFileId:guid}/qr.png")]
    [Produces("image/png")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadQrCode(Guid clientId, Guid certificateFileId, CancellationToken cancellationToken)
    {
        var file = await certificates.GetQrCodeAsync(clientId, certificateFileId, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
