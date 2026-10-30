namespace Inspecta.Application.Contracts.Certificates;

/// <summary>An issued certificate filed under a client. Download it with <c>GET /clients/{clientId}/certificates/{id}/pdf</c>.</summary>
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
    string VerificationCode);

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
    string? IssuedBy);
