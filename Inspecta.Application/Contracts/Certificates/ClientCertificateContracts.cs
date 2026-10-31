namespace Inspecta.Application.Contracts.Certificates;

/// <summary>
/// An issued certificate filed under a client. Download it with <c>GET /clients/{clientId}/certificates/{id}/pdf</c>.
/// <c>qrUrl</c> is what the certificate's QR code opens: the public check page, from which the client downloads the PDF.
/// </summary>
public sealed record ClientCertificateResponse(
    Guid Id,
    Guid RequestId,
    string CertificateNumber,
    string ItemName,
    DateOnly? ExaminationDate,
    DateOnly? NextExaminationDate,
    DateTimeOffset IssuedAt,
    string FileName,
    string Sha256,
    string VerificationCode,
    string? QrUrl = null,
    InspectionResult? Result = null);

/// <summary>What the public page shows when someone scans a certificate's QR code.</summary>
public sealed record CertificateVerificationResponse(
    bool Valid,
    string Status,
    string? CertificateNumber,
    string? ItemName,
    string? ClientName,
    DateOnly? ExaminationDate,
    DateOnly? NextExaminationDate,
    DateTimeOffset? IssuedAt,
    string? IssuedBy,
    string? PdfUrl = null,
    InspectionResult? Result = null);
