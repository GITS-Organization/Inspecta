namespace Inspecta.Application.Pdf.Models;

/// <summary>
/// Everything printed on one machine's Report of Thorough Examination. <c>QrData</c> is what the QR code opens; it is
/// given when the inspector first submits, so the preview carries it, and empty before. <c>Conclusion</c> is the verdict,
/// one word: Satisfactory or Unsatisfactory.
/// </summary>
public sealed record CertificatePdfModel(
    string? CertificateNumber,
    DateTimeOffset? CertificateDate,
    bool IsDraft,
    string ClientName,
    string ClientAddress,
    string? ClientRepresentative,
    DateOnly? LastExaminationDate,
    string? LastCertificateNumber,
    string? LastCertificateBy,
    DateOnly? ExaminationDate,
    DateOnly? NextExaminationDate,
    string ItemName,
    IReadOnlyList<string> Standards,
    string? TestType,
    string Conclusion,
    IReadOnlyList<CertificatePdfField> Fields,
    CertificatePdfQuestionnaire Questionnaire,
    IReadOnlyList<CertificatePdfSection> Checklist,
    string? PreparedByName,
    string? PreparedByQualifications,
    string? SignedBy,
    string? SignatureStrokesJson,
    string? QrData = null);

/// <summary>A label and its value; a missing value prints as a dash.</summary>
public sealed record CertificatePdfField(string Label, string? Value);

public sealed record CertificatePdfQuestionnaire(
    bool? FirstExamination,
    bool? InstalledCorrectly,
    bool? Within6Months,
    bool? Within12Months,
    bool? ExaminationScheme,
    bool? ExceptionalCircumstances,
    string DefectDescription,
    bool? ExistingDanger,
    bool? FutureDanger,
    DateOnly? FutureDangerBy,
    string RepairsRequired,
    string TestsCarriedOut,
    string? Note);

/// <summary><c>Value</c> is <c>Yes</c>, <c>No</c>, <c>N/A</c> or empty.</summary>
public sealed record CertificatePdfRow(string Label, string? Value, string? Note);

public sealed record CertificatePdfSection(string Title, IReadOnlyList<CertificatePdfRow> Rows);
