using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Certificates;

/// <summary>Current draft or reviewed inspection certificate.</summary>
public sealed record CertificateResponse(
    Guid Id,
    Guid RequestId,
    Guid? RequestItemId,
    Guid? EquipmentId,
    Guid InspectorId,
    string TemplateId,
    IReadOnlyDictionary<string, string> Texts,
    IReadOnlyDictionary<string, DateTimeOffset> Dates,
    IReadOnlyDictionary<string, bool> Answers,
    ChecklistAnswer FunctionCheck,
    CertificateResult? FinalResult,
    DateTimeOffset? SubmittedAt,
    string? ReviewNote,
    string? CertificateNumber,
    string? VerificationCode,
    DateTimeOffset? ReviewedAt);