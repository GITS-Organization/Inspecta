using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Text.Json;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

public sealed class InspectionCertificate
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private InspectionCertificate() { }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid? EquipmentId { get; private set; }
    public Guid InspectorId { get; private set; }
    public string TemplateId { get; private set; } = "TUV-RTE-01";
    public string TextsJson { get; private set; } = "{}";
    public string DatesJson { get; private set; } = "{}";
    public string AnswersJson { get; private set; } = "{}";
    public string SignatureStrokesJson { get; private set; } = "[]";
    public ChecklistAnswer FunctionCheck { get; private set; }
    public bool HasAngle { get; private set; }
    public CertificateResult? FinalResult { get; private set; }
    public string? ReviewNote { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public string? ReviewerComments { get; private set; }
    public Guid? ReviewerId { get; private set; }
    public string? ReviewerName { get; private set; }
    public string? ReviewerLicense { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? CertificateNumber { get; private set; }
    public string? VerificationCode { get; private set; }
    public string? QrData { get; private set; }
    public string IssuedForJson { get; private set; } = "{}";
    [NotMapped]
    public CertificateIssueSnapshot? IssuedFor
    {
        get => ReadIssuedFor();
        private set => IssuedForJson = JsonSerializer.Serialize(value, JsonOptions);
    }
    public string? SentTo { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }

    [NotMapped]
    public CertificateDraft Draft => ReadDraft();

    public static InspectionCertificate Create(
        Guid requestId,
        Guid inspectorId,
        Guid? equipmentId = null,
        bool hasAngle = false)
    {
        if (requestId == Guid.Empty || inspectorId == Guid.Empty)
            throw new ArgumentException("Request and inspector are required.");
        if (equipmentId == Guid.Empty)
            throw new ArgumentException("Equipment id cannot be empty.", nameof(equipmentId));

        return new InspectionCertificate
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            EquipmentId = equipmentId,
            InspectorId = inspectorId,
            HasAngle = hasAngle
        };
    }

    public void SetDraft(CertificateDraft draft)
    {
        EnsureEditable();
        ArgumentNullException.ThrowIfNull(draft);
        if (!HasAngle && draft.AngleReadingDegrees is not null)
            throw new ArgumentException("Angle reading is not applicable to this equipment.", nameof(draft));
        if (draft.AngleReadingDegrees is { } angleReading &&
            (angleReading is < 0m or > 360m || decimal.Round(angleReading, 2) != angleReading))
            throw new ArgumentOutOfRangeException(nameof(draft), "Angle reading must be between 0 and 360 degrees with at most two decimals.");

        draft = draft with
        {
            InstalledCorrectly = draft.FirstExamination == true ? draft.InstalledCorrectly : null,
            FutureDangerBy = draft.FutureDanger == true ? draft.FutureDangerBy : null,
            Within12Months = draft.Within12Months ??
                (draft.Within6Months is { } within6Months ? !within6Months : null),
            Within6Months = draft.Within6Months ??
                (draft.Within12Months is { } within12Months ? !within12Months : null)
        };
        if (draft.Within6Months is { } within6 &&
            draft.Within12Months is { } within12 &&
            within6 == within12)
            throw new ArgumentException("Within six months and within twelve months must be opposites.", nameof(draft));

        var texts = new Dictionary<string, string>();
        AddText(texts, "clientRepresentative", draft.ClientRepresentative);
        AddText(texts, "standardOfInspection", draft.StandardOfInspection);
        AddText(texts, "testType", draft.TestType);
        AddText(texts, "manufacturer", draft.Manufacturer);
        AddText(texts, "modelYear", draft.ModelYear);
        AddText(texts, "maxWorkingRate", draft.MaxWorkingRate);
        AddText(texts, "serialNumber", draft.SerialNumber);
        AddText(texts, "ownerId", draft.OwnerId);
        AddText(texts, "ndt", draft.Ndt);
        AddText(texts, "defectDescription", draft.DefectDescription);
        AddText(texts, "repairsRequired", draft.RepairsRequired);
        AddText(texts, "testsCarriedOut", draft.TestsCarriedOut);
        AddText(texts, "note", draft.Note);
        AddText(texts, "preparedByName", draft.PreparedByName);
        AddText(texts, "preparedByQualifications", draft.PreparedByQualifications);
        if (draft.AngleReadingDegrees is { } angleValue)
            texts["angleReadingDegrees"] = angleValue.ToString(CultureInfo.InvariantCulture);

        var dates = new Dictionary<string, DateTimeOffset>();
        AddDate(dates, "examination", draft.ExaminationDate);
        AddDate(dates, "nextExamination", draft.NextExaminationDate);
        AddDate(dates, "futureDangerBy", draft.FutureDangerBy);

        var answers = new Dictionary<string, bool>();
        AddAnswer(answers, "firstExamination", draft.FirstExamination);
        AddAnswer(answers, "installedCorrectly", draft.InstalledCorrectly);
        AddAnswer(answers, "within6Months", draft.Within6Months);
        AddAnswer(answers, "within12Months", draft.Within12Months);
        AddAnswer(answers, "examinationScheme", draft.ExaminationScheme);
        AddAnswer(answers, "exceptionalCircumstances", draft.ExceptionalCircumstances);
        AddAnswer(answers, "existingDanger", draft.ExistingDanger);
        AddAnswer(answers, "futureDanger", draft.FutureDanger);

        TextsJson = JsonSerializer.Serialize(texts);
        DatesJson = JsonSerializer.Serialize(dates);
        AnswersJson = JsonSerializer.Serialize(answers);
        FunctionCheck = draft.FunctionCheck;
        FinalResult = draft.FinalResult;
    }

    public void SetDraft(
        IReadOnlyDictionary<string, string> texts,
        IReadOnlyDictionary<string, DateTimeOffset> dates,
        IReadOnlyDictionary<string, bool> answers,
        ChecklistAnswer functionCheck,
        CertificateResult? finalResult)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(answers);

        SetDraft(new CertificateDraft
        {
            ClientRepresentative = GetText(texts, "clientRepresentative"),
            StandardOfInspection = GetText(texts, "standardOfInspection"),
            TestType = GetText(texts, "testType"),
            Manufacturer = GetText(texts, "manufacturer"),
            ModelYear = GetText(texts, "modelYear"),
            MaxWorkingRate = GetText(texts, "maxWorkingRate"),
            SerialNumber = GetText(texts, "serialNumber"),
            OwnerId = GetText(texts, "ownerId"),
            Ndt = GetText(texts, "ndt"),
            DefectDescription = GetText(texts, "defectDescription"),
            RepairsRequired = GetText(texts, "repairsRequired"),
            TestsCarriedOut = GetText(texts, "testsCarriedOut"),
            Note = GetText(texts, "note"),
            PreparedByName = GetText(texts, "preparedByName"),
            PreparedByQualifications = GetText(texts, "preparedByQualifications"),
            ExaminationDate = GetDate(dates, "examination"),
            NextExaminationDate = GetDate(dates, "nextExamination"),
            FutureDangerBy = GetDate(dates, "futureDangerBy"),
            FirstExamination = GetAnswer(answers, "firstExamination"),
            InstalledCorrectly = GetAnswer(answers, "installedCorrectly"),
            Within6Months = GetAnswer(answers, "within6Months"),
            Within12Months = GetAnswer(answers, "within12Months"),
            ExaminationScheme = GetAnswer(answers, "examinationScheme"),
            ExceptionalCircumstances = GetAnswer(answers, "exceptionalCircumstances"),
            ExistingDanger = GetAnswer(answers, "existingDanger"),
            FutureDanger = GetAnswer(answers, "futureDanger"),
            AngleReadingDegrees = GetText(texts, "angleReadingDegrees") is { } angleText &&
                decimal.TryParse(angleText, NumberStyles.Number, CultureInfo.InvariantCulture, out var angle)
                    ? angle
                    : null,
            FunctionCheck = functionCheck,
            FinalResult = finalResult
        });
    }

    public bool IsReadyToSubmit() => Draft.IsReadyToSubmit(HasAngle);

    public void Submit()
    {
        EnsureEditable();
        if (!IsReadyToSubmit())
            throw new InvalidOperationException("Required certificate fields are incomplete or invalid.");
        SubmittedAt = DateTimeOffset.UtcNow;
        ReviewedAt = null;
        ReviewerId = null;
        ReviewerName = null;
        ReviewerLicense = null;
        ReviewNote = null;
        ReviewerComments = null;
    }

    public void Submit(string preparedByName, IEnumerable<UserQualification> qualifications)
    {
        if (string.IsNullOrWhiteSpace(preparedByName))
            throw new ArgumentException("Inspector name is required.", nameof(preparedByName));
        ArgumentNullException.ThrowIfNull(qualifications);
        var qualificationNames = qualifications.Select(item => item.Name.Trim()).Where(name => name.Length > 0).ToArray();
        var draft = Draft with
        {
            PreparedByName = preparedByName.Trim(),
            PreparedByQualifications = string.Join(", ", qualificationNames)
        };
        if (!draft.IsReadyToSubmit(HasAngle))
            throw new InvalidOperationException("Required certificate fields are incomplete or invalid.");
        SetDraft(draft);
        Submit();
    }

    public void ReturnForChanges(Guid reviewerId, string note, string? reviewerName = null, string? reviewerLicense = null)
    {
        EnsureSubmitted();
        if (reviewerId == Guid.Empty)
            throw new ArgumentException("Reviewer is required.", nameof(reviewerId));
        if (string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("A review note is required.", nameof(note));
        ReviewerId = reviewerId;
        ReviewerName = reviewerName?.Trim();
        ReviewerLicense = reviewerLicense?.Trim();
        ReviewerComments = note.Trim();
        ReviewNote = note.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
    }

    public void Approve(
        Guid reviewerId,
        string certificateNumber,
        string verificationCode,
        string sentTo,
        string signatureStrokesJson,
        string? reviewerName = null,
        string? reviewerLicense = null,
        string? qrData = null,
        DateTimeOffset? sentAt = null,
        CertificateIssueSnapshot? issuedFor = null)
    {
        EnsureSubmitted();
        if (reviewerId == Guid.Empty)
            throw new ArgumentException("Reviewer is required.", nameof(reviewerId));
        if (string.IsNullOrWhiteSpace(certificateNumber) || string.IsNullOrWhiteSpace(verificationCode))
            throw new ArgumentException("Certificate number and verification code are required.");
        if (string.IsNullOrWhiteSpace(sentTo))
            throw new ArgumentException("Client recipient is required.", nameof(sentTo));
        if (issuedFor is not null)
        {
            issuedFor = CertificateIssueSnapshot.Create(
                issuedFor.ClientName,
                issuedFor.Location,
                issuedFor.Item,
                issuedFor.SerialNumber);
        }
        ValidateSignature(signatureStrokesJson);

        SignatureStrokesJson = signatureStrokesJson;
        CertificateNumber = certificateNumber.Trim();
        VerificationCode = verificationCode.Trim();
        SentTo = sentTo.Trim();
        ReviewerId = reviewerId;
        ReviewerName = reviewerName?.Trim();
        ReviewerLicense = reviewerLicense?.Trim();
        QrData = qrData?.Trim();
        IssuedFor = issuedFor;
        SentAt = sentAt;
        ReviewedAt = DateTimeOffset.UtcNow;
        ReviewNote = null;
    }

    private CertificateDraft ReadDraft()
    {
        var texts = Deserialize<Dictionary<string, string>>(TextsJson);
        var dates = Deserialize<Dictionary<string, DateTimeOffset>>(DatesJson);
        var answers = Deserialize<Dictionary<string, bool>>(AnswersJson);
        return new CertificateDraft
        {
            ClientRepresentative = GetText(texts, "clientRepresentative"),
            StandardOfInspection = GetText(texts, "standardOfInspection"),
            TestType = GetText(texts, "testType"),
            Manufacturer = GetText(texts, "manufacturer"),
            ModelYear = GetText(texts, "modelYear"),
            MaxWorkingRate = GetText(texts, "maxWorkingRate"),
            SerialNumber = GetText(texts, "serialNumber"),
            OwnerId = GetText(texts, "ownerId"),
            Ndt = GetText(texts, "ndt"),
            DefectDescription = GetText(texts, "defectDescription"),
            RepairsRequired = GetText(texts, "repairsRequired"),
            TestsCarriedOut = GetText(texts, "testsCarriedOut"),
            Note = GetText(texts, "note"),
            PreparedByName = GetText(texts, "preparedByName"),
            PreparedByQualifications = GetText(texts, "preparedByQualifications"),
            ExaminationDate = GetDate(dates, "examination"),
            NextExaminationDate = GetDate(dates, "nextExamination"),
            FutureDangerBy = GetDate(dates, "futureDangerBy"),
            FirstExamination = GetAnswer(answers, "firstExamination"),
            InstalledCorrectly = GetAnswer(answers, "installedCorrectly"),
            Within6Months = GetAnswer(answers, "within6Months"),
            Within12Months = GetAnswer(answers, "within12Months"),
            ExaminationScheme = GetAnswer(answers, "examinationScheme"),
            ExceptionalCircumstances = GetAnswer(answers, "exceptionalCircumstances"),
            ExistingDanger = GetAnswer(answers, "existingDanger"),
            FutureDanger = GetAnswer(answers, "futureDanger"),
            AngleReadingDegrees = GetText(texts, "angleReadingDegrees") is { } angleText &&
                decimal.TryParse(angleText, NumberStyles.Number, CultureInfo.InvariantCulture, out var angle)
                    ? angle
                    : null,
            FunctionCheck = FunctionCheck,
            FinalResult = FinalResult
        };
    }

    private CertificateIssueSnapshot? ReadIssuedFor()
    {
        using var document = JsonDocument.Parse(IssuedForJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.EnumerateObject().Any())
            return null;
        return JsonSerializer.Deserialize<CertificateIssueSnapshot>(IssuedForJson, JsonOptions);
    }

    private void EnsureEditable()
    {
        if (SubmittedAt is not null && ReviewedAt is null)
            throw new InvalidOperationException("Submitted certificate is locked while under review.");
        if (CertificateNumber is not null)
            throw new InvalidOperationException("Approved certificate is locked.");
    }

    private void EnsureSubmitted()
    {
        if (CertificateNumber is not null)
            throw new InvalidOperationException("Certificate is already approved.");
        if (SubmittedAt is null)
            throw new InvalidOperationException("Only submitted certificates can be reviewed.");
        if (ReviewedAt is not null)
            throw new InvalidOperationException("A returned certificate must be resubmitted before another review.");
    }

    private static void AddText(IDictionary<string, string> fields, string key, string? value)
    {
        if (value is not null)
            fields[key] = value.Trim();
    }

    private static void AddDate(IDictionary<string, DateTimeOffset> fields, string key, DateOnly? value)
    {
        if (value is { } date)
            fields[key] = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    private static void AddAnswer(IDictionary<string, bool> fields, string key, bool? value)
    {
        if (value is { } answer)
            fields[key] = answer;
    }

    private static string? GetText(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value : null;

    private static DateOnly? GetDate(IReadOnlyDictionary<string, DateTimeOffset> fields, string key) =>
        fields.TryGetValue(key, out var value) ? DateOnly.FromDateTime(value.UtcDateTime) : null;

    private static bool? GetAnswer(IReadOnlyDictionary<string, bool> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value : null;

    private static void ValidateSignature(string signatureStrokesJson)
    {
        try
        {
            using var document = JsonDocument.Parse(signatureStrokesJson);
            var strokes = document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray().ToArray()
                : [];
            if (strokes.Length == 0 || strokes.Any(stroke => !IsValidStroke(stroke)))
            {
                throw new ArgumentException("Reviewer signature must contain normalized x/y strokes.", nameof(signatureStrokesJson));
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Reviewer signature is not valid JSON.", nameof(signatureStrokesJson), exception);
        }
    }

    private static bool IsValidStroke(JsonElement stroke)
    {
        JsonElement points;
        if (stroke.ValueKind == JsonValueKind.Array)
        {
            points = stroke;
        }
        else if (stroke.ValueKind == JsonValueKind.Object &&
            stroke.TryGetProperty("p", out var encodedPoints) &&
            encodedPoints.ValueKind == JsonValueKind.Array)
        {
            points = encodedPoints;
        }
        else
        {
            return false;
        }

        if (points.GetArrayLength() < 4 || points.GetArrayLength() % 2 != 0)
            return false;

        return points.EnumerateArray().All(point =>
            point.ValueKind == JsonValueKind.Number && point.GetDouble() is >= 0 and <= 1);
    }

    private static T Deserialize<T>(string json) where T : new() =>
        JsonSerializer.Deserialize<T>(json) ?? new T();
}
