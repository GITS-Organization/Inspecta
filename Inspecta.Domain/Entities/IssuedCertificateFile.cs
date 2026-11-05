using System.Security.Cryptography;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

/// <summary>
/// The final PDF of an approved certificate, kept exactly as it was issued (with its SHA-256) and filed under the
/// client. Later changes to templates or company details never alter it. <see cref="QrData"/> keeps what the QR printed
/// on it opens (the public check page with the PDF download), so it can also be sent to the client as an image.
/// </summary>
public sealed class IssuedCertificateFile
{
    private IssuedCertificateFile() { }

    public Guid Id { get; private set; }
    public Guid CertificateId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid ManagerId { get; private set; }
    public Guid? ClientId { get; private set; }
    public Guid? EquipmentId { get; private set; }
    public string ClientName { get; private set; } = string.Empty;
    public string ItemName { get; private set; } = string.Empty;
    public string CertificateNumber { get; private set; } = string.Empty;
    public string VerificationCode { get; private set; } = string.Empty;
    public string? QrData { get; private set; }
    public string? PreparedBy { get; private set; }

    /// <summary>The inspector's verdict printed on it: <c>SafeToOperate</c> (Satisfactory) or <c>NotSafe</c> (Unsatisfactory).</summary>
    public CertificateResult? Result { get; private set; }
    public DateOnly? ExaminationDate { get; private set; }
    public DateOnly? NextExaminationDate { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    /// <summary>The PDF's bytes live in their own table (<see cref="IssuedCertificateContent"/>) so that lists of certificates never read them.</summary>
    public IssuedCertificateContent? Body { get; private set; }

    /// <summary>The PDF itself. Load the file with its body (<c>Include</c>) before reading this; <see cref="ContentLength"/> is always available.</summary>
    public byte[] Content => Body?.Bytes ?? throw new InvalidOperationException("The certificate file was loaded without its content.");

    public int ContentLength { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; }

    /// <summary>Replaces the file after the certificate number or date was changed before the handover: the same certificate, drawn again.</summary>
    public void Reissue(string certificateNumber, DateTimeOffset issuedAt, string fileName, byte[] content)
    {
        if (string.IsNullOrWhiteSpace(certificateNumber)) throw new ArgumentException("Certificate number is required.", nameof(certificateNumber));
        if (content is null || content.Length == 0) throw new ArgumentException("The certificate file is empty.", nameof(content));
        CertificateNumber = certificateNumber.Trim();
        IssuedAt = issuedAt;
        FileName = fileName;
        if (Body is null) throw new InvalidOperationException("Load the certificate file with its content before drawing it again.");
        Body.Replace(content);
        ContentLength = content.Length;
        Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

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
        DateTimeOffset? issuedAt = null,
        Guid? equipmentId = null,
        string? qrData = null,
        string? preparedBy = null,
        CertificateResult? result = null)
    {
        if (certificateId == Guid.Empty || requestId == Guid.Empty)
            throw new ArgumentException("Certificate and request are required.");
        if (string.IsNullOrWhiteSpace(certificateNumber) || string.IsNullOrWhiteSpace(verificationCode))
            throw new ArgumentException("Certificate number and verification code are required.");
        if (content is null || content.Length == 0) throw new ArgumentException("The certificate file is empty.", nameof(content));

        var id = Guid.NewGuid();
        return new IssuedCertificateFile
        {
            Id = id,
            CertificateId = certificateId,
            RequestId = requestId,
            ManagerId = managerId,
            ClientId = clientId,
            EquipmentId = equipmentId,
            ClientName = clientName.Trim(),
            ItemName = itemName.Trim(),
            CertificateNumber = certificateNumber.Trim(),
            VerificationCode = verificationCode.Trim(),
            QrData = string.IsNullOrWhiteSpace(qrData) ? null : qrData.Trim(),
            PreparedBy = string.IsNullOrWhiteSpace(preparedBy) ? null : preparedBy.Trim(),
            Result = result,
            ExaminationDate = examinationDate,
            NextExaminationDate = nextExaminationDate,
            FileName = fileName,
            Body = new IssuedCertificateContent(id, content),
            ContentLength = content.Length,
            Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            IssuedAt = issuedAt ?? DateTimeOffset.UtcNow
        };
    }
}
