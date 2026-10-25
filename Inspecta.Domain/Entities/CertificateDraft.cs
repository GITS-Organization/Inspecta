using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

public sealed record CertificateDraft
{
    public string? ClientRepresentative { get; init; }
    public string? StandardOfInspection { get; init; }
    public string? TestType { get; init; }
    public DateOnly? ExaminationDate { get; init; }
    public DateOnly? NextExaminationDate { get; init; }
    public string? Manufacturer { get; init; }
    public string? ModelYear { get; init; }
    public string? MaxWorkingRate { get; init; }
    public string? SerialNumber { get; init; }
    public string? OwnerId { get; init; }
    public string? Ndt { get; init; }
    public ChecklistAnswer FunctionCheck { get; init; } = ChecklistAnswer.Unanswered;
    public decimal? AngleReadingDegrees { get; init; }
    public bool? FirstExamination { get; init; }
    public bool? InstalledCorrectly { get; init; }
    public bool? Within6Months { get; init; }
    public bool? Within12Months { get; init; }
    public bool? ExaminationScheme { get; init; }
    public bool? ExceptionalCircumstances { get; init; }
    public bool? ExistingDanger { get; init; }
    public bool? FutureDanger { get; init; }
    public DateOnly? FutureDangerBy { get; init; }
    public string? DefectDescription { get; init; }
    public string? RepairsRequired { get; init; }
    public string? TestsCarriedOut { get; init; }
    public string? Note { get; init; }
    public string? PreparedByName { get; init; }
    public string? PreparedByQualifications { get; init; }
    public CertificateResult? FinalResult { get; init; }

    public CertificateDraft SetWithin6Months(bool value) =>
        this with { Within6Months = value, Within12Months = !value };

    public CertificateDraft SetWithin12Months(bool value) =>
        this with { Within12Months = value, Within6Months = !value };

    public bool IsReadyToSubmit(bool hasAngle)
    {
        var hasRequiredDetails =
            !string.IsNullOrWhiteSpace(StandardOfInspection) &&
            !string.IsNullOrWhiteSpace(TestType) &&
            ExaminationDate is not null &&
            NextExaminationDate is not null &&
            NextExaminationDate > ExaminationDate &&
            Enum.IsDefined(FunctionCheck) &&
            FunctionCheck != ChecklistAnswer.Unanswered &&
            FinalResult is { } result && Enum.IsDefined(result);

        var hasRequiredAnswers =
            FirstExamination is not null &&
            (FirstExamination != true || InstalledCorrectly is not null) &&
            Within6Months is not null &&
            Within12Months is not null &&
            Within6Months != Within12Months &&
            ExaminationScheme is not null &&
            ExceptionalCircumstances is not null &&
            ExistingDanger is not null &&
            FutureDanger is not null &&
            (FutureDanger != true || FutureDangerBy is not null);

        var angleIsValid = hasAngle
            ? AngleReadingDegrees is >= 0m and <= 360m && decimal.Round(AngleReadingDegrees.Value, 2) == AngleReadingDegrees.Value
            : AngleReadingDegrees is null;

        return hasRequiredDetails && hasRequiredAnswers && angleIsValid;
    }
}
