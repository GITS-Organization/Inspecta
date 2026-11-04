using System.ComponentModel.DataAnnotations;

namespace Inspecta.Application.Contracts.Certificates;

/// <summary>The number and the date printed on one approved certificate, as the coordinator wants them.</summary>
public sealed record CertificateDetail
{
    [Required]
    public Guid RequestItemId { get; init; }

    /// <summary>"Certificate No." as printed (for example <c>CERT-2026-0008</c>); at most 64 characters and not used by another certificate of the team.</summary>
    [Required, MaxLength(64)]
    public string CertificateNumber { get; init; } = string.Empty;

    /// <summary>"Certificate Date" as printed; not the examination date, which stays as the inspector wrote it.</summary>
    [Required]
    public DateOnly CertificateDate { get; init; }
}

/// <summary>The numbers and dates of the certificates of a job that is ready to be handed over.</summary>
public sealed record UpdateCertificateDetailsRequest
{
    public IReadOnlyList<CertificateDetail> Certificates { get; init; } = [];
}
