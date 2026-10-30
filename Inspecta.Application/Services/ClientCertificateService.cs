using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf;

namespace Inspecta.Application.Services;

/// <summary>The certificates filed under a client, and the public check behind a certificate's QR code.</summary>
public sealed class ClientCertificateService(
    IClientRepository clientRepository,
    ICertificateFileRepository certificateFiles,
    IQrCodeRenderer? qrCodes = null)
{
    public const string Issuer = "Integrity International Inspection";

    public async Task<IReadOnlyList<ClientCertificateResponse>> ListAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        if (await clientRepository.GetByIdAsync(clientId, tracking: false, cancellationToken) is null)
            throw new KeyNotFoundException("Client was not found.");
        return (await certificateFiles.ListForClientAsync(clientId, cancellationToken))
            .Select(file => new ClientCertificateResponse(
                file.Id, file.RequestId, file.CertificateNumber, file.ItemName, file.ExaminationDate, file.NextExaminationDate,
                file.IssuedAt, file.FileName, file.Sha256, file.VerificationCode, file.QrData))
            .ToArray();
    }

    public async Task<PdfFile> GetPdfAsync(Guid clientId, Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await certificateFiles.GetForClientAsync(clientId, fileId, cancellationToken)
            ?? throw new KeyNotFoundException("Certificate was not found.");
        return new PdfFile(file.Content, file.FileName);
    }

    /// <summary>The QR code of an issued certificate (the one printed on it) as a PNG, to send to the client.</summary>
    public async Task<DownloadFile> GetQrCodeAsync(Guid clientId, Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await certificateFiles.GetForClientAsync(clientId, fileId, cancellationToken)
            ?? throw new KeyNotFoundException("Certificate was not found.");
        if (string.IsNullOrWhiteSpace(file.QrData) || qrCodes is null)
            throw new KeyNotFoundException("This certificate has no QR code.");
        return new DownloadFile(qrCodes.RenderPng(file.QrData), $"qr-{file.CertificateNumber}.png", "image/png");
    }

    /// <summary>
    /// Checks a code from a QR. <c>valid</c> says the certificate was issued by us; <c>status</c> is <c>valid</c> or, once its
    /// next examination date has passed, <c>expired</c>. Shows only what is printed on the certificate itself.
    /// </summary>
    public async Task<CertificateVerificationResponse> VerifyAsync(string? code, DateOnly today, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(code);
        if (normalized is null)
            return Unknown();
        var file = await certificateFiles.FindByVerificationCodeAsync(normalized, cancellationToken);
        if (file is null) return Unknown();
        var expired = file.NextExaminationDate is { } next && next < today;
        return new CertificateVerificationResponse(
            true,
            expired ? "expired" : "valid",
            file.CertificateNumber,
            file.ItemName,
            file.ClientName,
            file.ExaminationDate,
            file.NextExaminationDate,
            file.IssuedAt,
            Issuer,
            PdfPath(normalized));
    }

    /// <summary>
    /// The issued PDF behind a QR code, for the public page's download button. Anyone holding the code may download it:
    /// the code is 16 random characters and the route is rate limited.
    /// </summary>
    public async Task<PdfFile?> GetPdfByCodeAsync(string? code, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(code);
        if (normalized is null) return null;
        var file = await certificateFiles.GetByVerificationCodeAsync(normalized, cancellationToken);
        return file is null ? null : new PdfFile(file.Content, file.FileName);
    }

    /// <summary>Where the public page downloads the certificate from.</summary>
    public static string PdfPath(string code) => $"/verify/{Uri.EscapeDataString(code)}/pdf";

    private static string? Normalize(string? code) =>
        code?.Trim().ToUpperInvariant() is { Length: >= 8 and <= 64 } normalized ? normalized : null;

    private static CertificateVerificationResponse Unknown() =>
        new(false, "not_found", null, null, null, null, null, null, null);
}
