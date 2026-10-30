using System.Security.Cryptography;

namespace Inspecta.Domain.Entities;

/// <summary>
/// The final PDF of an approved certificate, kept exactly as it was issued (with its SHA-256) and filed under the
/// client. Later changes to templates or company details never alter it.
/// </summary>
public sealed class IssuedCertificateFile
{
    private IssuedCertificateFile() { }

    public Guid Id { get; private set; }
    public Guid CertificateId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid ManagerId { get; private set; }
    public Guid? ClientId { get; private set; }
    public string ClientName { get; private set; } = string.Empty;
    public string ItemName { get; private set; } = string.Empty;
    public string CertificateNumber { get; private set; } = string.Empty;
    public string VerificationCode { get; private set; } = string.Empty;
    public DateOnly? ExaminationDate { get; private set; }
    public DateOnly? NextExaminationDate { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public byte[] Content { get; private set; } = [];
    public string Sha256 { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; }

    public static IssuedCertificateFile Create(
        Guid certificateId,
        Guid requestId,
        Guid managerId,
        Guid? clientId,
        string clientName,
        string itemName,
        string certificateNumber,
        string verificationCode,
        DateOnly? examinationDate,
        DateOnly? nextExaminationDate,
        string fileName,
        byte[] content,
        DateTimeOffset? issuedAt = null)
    {
        if (certificateId == Guid.Empty || requestId == Guid.Empty)
            throw new ArgumentException("Certificate and request are required.");
        if (string.IsNullOrWhiteSpace(certificateNumber) || string.IsNullOrWhiteSpace(verificationCode))
            throw new ArgumentException("Certificate number and verification code are required.");
        if (content is null || content.Length == 0) throw new ArgumentException("The certificate file is empty.", nameof(content));

        return new IssuedCertificateFile
        {
            Id = Guid.NewGuid(),
            CertificateId = certificateId,
            RequestId = requestId,
            ManagerId = managerId,
            ClientId = clientId,
            ClientName = clientName.Trim(),
            ItemName = itemName.Trim(),
            CertificateNumber = certificateNumber.Trim(),
            VerificationCode = verificationCode.Trim(),
            ExaminationDate = examinationDate,
            NextExaminationDate = nextExaminationDate,
            FileName = fileName,
            Content = content,
            Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            IssuedAt = issuedAt ?? DateTimeOffset.UtcNow
        };
    }
}
