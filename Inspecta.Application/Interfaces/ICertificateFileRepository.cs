using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

/// <summary>An issued certificate without its PDF, for lists and the public verification page.</summary>
public sealed record IssuedCertificateSummary(
    Guid Id,
    Guid CertificateId,
    Guid RequestId,
    Guid? ClientId,
    string ClientName,
    string ItemName,
    string CertificateNumber,
    string VerificationCode,
    DateOnly? ExaminationDate,
    DateOnly? NextExaminationDate,
    string FileName,
    string Sha256,
    DateTimeOffset IssuedAt);

/// <summary>The issued PDFs filed under each client, and the numbering of certificates.</summary>
public interface ICertificateFileRepository
{
    /// <summary>Saves the file together with every other pending change, in one transaction.</summary>
    Task AddAsync(IssuedCertificateFile file, CancellationToken cancellationToken = default);

    Task<IssuedCertificateFile?> GetByCertificateIdAsync(Guid certificateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssuedCertificateFile>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>The certificates filed under a client, newest first (without the PDF itself).</summary>
    Task<IReadOnlyList<IssuedCertificateSummary>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken = default);

    Task<IssuedCertificateFile?> GetForClientAsync(Guid clientId, Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>Looks a certificate up by the code in its QR, ignoring the team filter: the public page has no signed-in user.</summary>
    Task<IssuedCertificateSummary?> FindByVerificationCodeAsync(string verificationCode, CancellationToken cancellationToken = default);

    /// <summary>Takes the next certificate number of the year for a team, for example <c>CERT-2026-0042</c>.</summary>
    Task<string> NextCertificateNumberAsync(Guid managerId, int year, CancellationToken cancellationToken = default);
}

/// <summary>Emails that carried certificates to clients.</summary>
public interface ICertificateDeliveryRepository
{
    Task<CertificateDelivery?> GetSentAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<CertificateDelivery?> GetPendingAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task AddAsync(CertificateDelivery delivery, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
