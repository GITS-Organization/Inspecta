using System.Text.Json;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Entities;

public sealed class InspectionCertificate
{
    private static readonly string[] RequiredCarriedOutAnswers =
        ["within6Months", "within12Months", "examinationScheme", "exceptionalCircumstances"];

    private InspectionCertificate() { }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid InspectorId { get; private set; }
    public string TemplateId { get; private set; } = "TUV-RTE-01";
    public string TextsJson { get; private set; } = "{}";
    public string DatesJson { get; private set; } = "{}";
    public string AnswersJson { get; private set; } = "{}";
    public string SignatureStrokesJson { get; private set; } = "[]";
    public ChecklistAnswer FunctionCheck { get; private set; }
    public CertificateResult? FinalResult { get; private set; }
    public string? ReviewNote { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public string? ReviewerComments { get; private set; }
    public Guid? ReviewerId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? CertificateNumber { get; private set; }
    public string? VerificationCode { get; private set; }
    public string? SentTo { get; private set; }

    public static InspectionCertificate Create(Guid requestId, Guid inspectorId)
    {
        if (requestId == Guid.Empty || inspectorId == Guid.Empty) throw new ArgumentException("Request and inspector are required.");
        return new InspectionCertificate { Id = Guid.NewGuid(), RequestId = requestId, InspectorId = inspectorId };
    }

    public void SetDraft(
        IReadOnlyDictionary<string, string> texts,
        IReadOnlyDictionary<string, DateTimeOffset> dates,
        IReadOnlyDictionary<string, bool> answers,
        ChecklistAnswer functionCheck,
        CertificateResult? finalResult)
    {
        EnsureEditable();
        TextsJson = JsonSerializer.Serialize(texts.ToDictionary(pair => pair.Key, pair => pair.Value.Trim()));
        DatesJson = JsonSerializer.Serialize(dates);
        AnswersJson = JsonSerializer.Serialize(answers);
        FunctionCheck = functionCheck;
        FinalResult = finalResult;
    }

    public bool IsReadyToSubmit()
    {
        var texts = Deserialize<Dictionary<string, string>>(TextsJson);
        var dates = Deserialize<Dictionary<string, DateTimeOffset>>(DatesJson);
        var answers = Deserialize<Dictionary<string, bool>>(AnswersJson);
        var isFirstExamination = answers.TryGetValue("firstExamination", out var first) && first;
        return dates.ContainsKey("examination") && dates.ContainsKey("nextExamination") &&
            HasText(texts, "standardOfInspection") && HasText(texts, "testType") &&
            FunctionCheck != ChecklistAnswer.Unanswered && FinalResult is not null &&
            answers.ContainsKey("firstExamination") && (!isFirstExamination || answers.ContainsKey("installedCorrectly")) &&
            RequiredCarriedOutAnswers.All(answers.ContainsKey);
    }

    public void Submit()
    {
        EnsureEditable();
        if (!IsReadyToSubmit()) throw new InvalidOperationException("Required certificate fields are incomplete.");
        SubmittedAt = DateTimeOffset.UtcNow;
        ReviewedAt = null;
        ReviewerId = null;
        ReviewNote = null;
        ReviewerComments = null;
    }

    public void ReturnForChanges(Guid reviewerId, string note)
    {
        EnsureSubmitted();
        if (string.IsNullOrWhiteSpace(note)) throw new ArgumentException("A review note is required.", nameof(note));
        ReviewerId = reviewerId;
        ReviewerComments = note.Trim();
        ReviewNote = note.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
    }

    public void Approve(Guid reviewerId, string certificateNumber, string verificationCode, string sentTo, string signatureStrokesJson)
    {
        EnsureSubmitted();
        if (string.IsNullOrWhiteSpace(certificateNumber) || string.IsNullOrWhiteSpace(verificationCode))
            throw new ArgumentException("Certificate number and verification code are required.");
        if (string.IsNullOrWhiteSpace(sentTo)) throw new ArgumentException("Client recipient is required.", nameof(sentTo));
        ValidateSignature(signatureStrokesJson);
        SignatureStrokesJson = signatureStrokesJson;
        CertificateNumber = certificateNumber.Trim();
        VerificationCode = verificationCode.Trim();
        SentTo = sentTo.Trim();
        ReviewerId = reviewerId;
        ReviewedAt = DateTimeOffset.UtcNow;
        ReviewNote = null;
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
        if (SubmittedAt is null) throw new InvalidOperationException("Only submitted certificates can be reviewed.");
        if (CertificateNumber is not null) throw new InvalidOperationException("Certificate is already approved.");
    }

    private static bool HasText(Dictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);

    private static void ValidateSignature(string signatureStrokesJson)
    {
        try
        {
            using var document = JsonDocument.Parse(signatureStrokesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array || !document.RootElement.EnumerateArray().Any(stroke =>
                stroke.ValueKind == JsonValueKind.Array && stroke.GetArrayLength() >= 4 && stroke.GetArrayLength() % 2 == 0 &&
                stroke.EnumerateArray().All(point => point.ValueKind == JsonValueKind.Number && point.GetDouble() is >= 0 and <= 1)))
            {
                throw new ArgumentException("Reviewer signature must contain normalized x/y strokes.", nameof(signatureStrokesJson));
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Reviewer signature is not valid JSON.", nameof(signatureStrokesJson), exception);
        }
    }

    private static T Deserialize<T>(string json) where T : new() =>
        JsonSerializer.Deserialize<T>(json) ?? new T();
}