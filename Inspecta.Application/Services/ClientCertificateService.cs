using Inspecta.Application.Contracts.Certificates;
using Inspecta.Application.Interfaces;
using Inspecta.Application.Pdf;

namespace Inspecta.Application.Services;

/// <summary>The certificates filed under a client, and the public check behind a certificate's QR code.</summary>
public sealed class ClientCertificateService(
    IClientRepository clientRepository,
    ICertificateFileRepository certificateFiles)
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

    /// <summary>
    /// Checks a code from a QR. <c>valid</c> says the certificate was issued by us; <c>status</c> is <c>valid</c> or, once its
    /// next examination date has passed, <c>expired</c>. Shows only what is printed on the certificate itself.
    /// </summary>
    public async Task<CertificateVerificationResponse> VerifyAsync(string? code, DateOnly today, CancellationToken cancellationToken = default)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized) || normalized.Length is < 8 or > 64)
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
            Issuer);
    }

    private static CertificateVerificationResponse Unknown() =>
        new(false, "not_found", null, null, null, null, null, null, null);
}
