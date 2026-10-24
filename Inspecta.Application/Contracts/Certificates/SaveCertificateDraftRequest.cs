using System.ComponentModel.DataAnnotations;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Certificates;

/// <summary>Inspector-editable draft fields for the inspection report.</summary>
public sealed record SaveCertificateDraftRequest
{
    public IReadOnlyDictionary<string, string> Texts { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, DateTimeOffset> Dates { get; init; } = new Dictionary<string, DateTimeOffset>();
    public IReadOnlyDictionary<string, bool> Answers { get; init; } = new Dictionary<string, bool>();
    public ChecklistAnswer FunctionCheck { get; init; }
    public CertificateResult? FinalResult { get; init; }
}

/// <summary>Technical manager decision for a submitted certificate.</summary>
public sealed record ReviewCertificateRequest
{
    public required bool Approve { get; init; }

    [MaxLength(4000)]
    public string? Note { get; init; }

    [MaxLength(128)]
    public string? CertificateNumber { get; init; }

    [MaxLength(128)]
    public string? VerificationCode { get; init; }

    [MaxLength(320)]
    public string? SentTo { get; init; }

    public string? SignatureStrokesJson { get; init; }
}