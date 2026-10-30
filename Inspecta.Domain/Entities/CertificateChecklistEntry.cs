namespace Inspecta.Domain.Entities;

/// <summary>The inspector's answer for one checklist row: <c>yes</c>, <c>no</c> or <c>na</c>, with a note (required for <c>no</c>).</summary>
public sealed record CertificateChecklistEntry(string Value, string? Note = null);

/// <summary>The values a checklist row can take.</summary>
public static class ChecklistValues
{
    public const string Yes = "yes";
    public const string No = "no";
    public const string NotApplicable = "na";

    public static readonly IReadOnlyList<string> All = [Yes, No, NotApplicable];

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}
