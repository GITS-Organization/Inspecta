using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Certificates;

/// <summary>The questionnaire, texts and dates every certificate has, and the rules for completing them.</summary>
public static class MachineFormRules
{
    public const string DefaultConclusion = "Satisfactory";
    public const string DefaultDefects = "NONE";
    public const string DefaultRepairs = "N/A";
    public const string DefaultTests = "NONE";

    public static readonly IReadOnlySet<string> AnswerKeys = new HashSet<string>
    {
        "firstExamination", "installedCorrectly", "within6Months", "within12Months",
        "examinationScheme", "exceptionalCircumstances", "existingDanger", "futureDanger"
    };

    public static readonly IReadOnlySet<string> TextKeys = new HashSet<string>
    {
        "testType", "conclusion", "defectDescription", "repairsRequired", "testsCarriedOut", "note", "clientRepresentative"
    };

    public static readonly IReadOnlySet<string> DateKeys = new HashSet<string>
    {
        "examination", "nextExamination", "futureDangerBy"
    };

    /// <summary>What is still missing or wrong before the certificate can go to review. Empty when it is ready.</summary>
    public static IReadOnlyList<string> Problems(
        CertificateTemplate template,
        IReadOnlyDictionary<string, CertificateChecklistEntry> checklist,
        IReadOnlyDictionary<string, string> texts,
        IReadOnlyDictionary<string, DateTimeOffset> dates,
        IReadOnlyDictionary<string, bool> answers,
        CertificateResult? result)
    {
        var problems = new List<string>();

        if (result is null) problems.Add("the result (satisfactory or unsatisfactory)");

        if (!dates.TryGetValue("examination", out var examination)) problems.Add("the examination date");
        if (!dates.TryGetValue("nextExamination", out var next)) problems.Add("the next examination date");
        else if (dates.ContainsKey("examination") && DateOnly.FromDateTime(next.UtcDateTime) <= DateOnly.FromDateTime(examination.UtcDateTime))
            problems.Add("a next examination date after the examination date");

        void Need(string key, string label)
        {
            if (!answers.ContainsKey(key)) problems.Add(label);
        }

        Need("firstExamination", "the answer to \"first examination after installation\"");
        if (answers.GetValueOrDefault("firstExamination")) Need("installedCorrectly", "the answer to \"installed correctly\"");
        Need("within6Months", "the answer to \"within 6 months\"");
        Need("within12Months", "the answer to \"within 12 months\"");
        Need("examinationScheme", "the answer to \"in accordance with an examination scheme\"");
        Need("exceptionalCircumstances", "the answer to \"after exceptional circumstances\"");
        Need("existingDanger", "the answer to \"existing or imminent danger\"");
        Need("futureDanger", "the answer to \"could become a danger\"");
        if (answers.GetValueOrDefault("futureDanger") && !dates.ContainsKey("futureDangerBy"))
            problems.Add("the date by which the future danger must be fixed");

        var unanswered = template.ChecklistPaths.Count(path => !checklist.ContainsKey(path));
        if (unanswered > 0) problems.Add($"{unanswered} checklist row(s)");
        var withoutNote = checklist.Count(pair => pair.Value.Value == ChecklistValues.No && string.IsNullOrWhiteSpace(pair.Value.Note));
        if (withoutNote > 0) problems.Add($"a note for {withoutNote} checklist row(s) answered No");

        if ((NeedsExplanation(checklist, answers) || result == CertificateResult.NotSafe) && !HasText(texts, "defectDescription"))
            problems.Add("the description of the defect");

        return problems;
    }

    /// <summary>A "No" in the checklist or a danger answer means the inspector must describe the defect.</summary>
    public static bool NeedsExplanation(
        IReadOnlyDictionary<string, CertificateChecklistEntry> checklist,
        IReadOnlyDictionary<string, bool> answers) =>
        checklist.Values.Any(entry => entry.Value == ChecklistValues.No) ||
        answers.GetValueOrDefault("existingDanger") ||
        answers.GetValueOrDefault("futureDanger");

    /// <summary>
    /// The texts as they are printed: blanks become the usual wording ("NONE", "N/A"). The conclusion is left alone: the
    /// result is printed there, as one word: Satisfactory or Unsatisfactory.
    /// </summary>
    public static Dictionary<string, string> WithDefaults(IReadOnlyDictionary<string, string> texts)
    {
        var result = new Dictionary<string, string>(texts);
        void Default(string key, string value)
        {
            if (!HasText(result, key)) result[key] = value;
        }

        Default("defectDescription", DefaultDefects);
        Default("repairsRequired", DefaultRepairs);
        Default("testsCarriedOut", DefaultTests);
        return result;
    }

    private static bool HasText(IReadOnlyDictionary<string, string> texts, string key) =>
        texts.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);
}
