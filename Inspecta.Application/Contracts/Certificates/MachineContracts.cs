using System.ComponentModel.DataAnnotations;
using Inspecta.Domain.Enums;

namespace Inspecta.Application.Contracts.Certificates;

/// <summary>Where one machine's certificate stands.</summary>
public enum MachineStatus
{
    /// <summary>Nothing saved yet.</summary>
    NotStarted,

    /// <summary>The inspector is filling it in.</summary>
    Draft,

    /// <summary>Sent to the technical manager, waiting for review.</summary>
    Submitted,

    /// <summary>The technical manager sent it back with a note; the inspector fixes it and submits again.</summary>
    Returned,

    /// <summary>Approved by the technical manager; ready for the coordinator to hand to the client.</summary>
    Approved,

    /// <summary>The coordinator handed it to the client.</summary>
    Delivered
}

/// <summary>
/// One machine of an assigned job, with the state of its certificate. <c>canEdit</c> and <c>canReview</c> tell the current
/// user what they can do with it now. <c>requestItemId</c> is the <c>id</c> of the item in <c>GET /requests/{requestId}</c>.
/// </summary>
public sealed record MachineSummaryResponse(
    Guid RequestItemId,
    string Name,
    int Quantity,
    string? SerialNumber,
    string TemplateKey,
    MachineStatus Status,
    int ChecklistTotal,
    int ChecklistAnswered,
    string? ReviewNote,
    string? CertificateNumber,
    bool CanEdit,
    bool CanReview);

public sealed record MachineProgress(int Total, int Submitted, int Approved, int Delivered);

/// <summary>
/// The machines of one job and how far their certificates are. <c>canStartInspection</c> and <c>canDeliver</c> tell the
/// current user whether those buttons apply to the job now.
/// </summary>
public sealed record MachineListResponse(
    Guid RequestId,
    JobStatus RequestStatus,
    string ClientName,
    string Location,
    DateTimeOffset? ScheduledAt,
    MachineProgress Progress,
    bool CanStartInspection,
    bool CanDeliver,
    IReadOnlyList<MachineSummaryResponse> Machines);

public sealed record MachineFormHeader(
    string ClientName,
    string? ClientAddress,
    string? ClientRepresentative,
    string InspectedItem,
    IReadOnlyList<string> Standards,
    DateOnly? LastExaminationDate,
    DateOnly? NextExaminationDue,
    string? InspectorName);

/// <summary>A technical field: the saved value, or the machine's own detail when nothing was saved.</summary>
public sealed record MachineFormField(string Key, string Label, string? Value);

/// <summary><c>Key</c> is <c>section.item</c>: send it back in <c>checklist</c> when saving.</summary>
public sealed record MachineFormRow(string Key, string Label, string? Value, string? Note);

public sealed record MachineFormSection(string Key, string Title, IReadOnlyList<MachineFormRow> Items);

/// <summary>
/// Everything the inspector's screen needs for one machine, in one call. <c>missing</c> lists what still stops a submit
/// (empty when <c>readyToSubmit</c>); <c>canEdit</c> and <c>canReview</c> say what the current user can do now.
/// </summary>
public sealed record MachineFormResponse(
    Guid RequestId,
    Guid RequestItemId,
    JobStatus RequestStatus,
    MachineStatus Status,
    bool CanEdit,
    bool CanReview,
    bool ReadyToSubmit,
    IReadOnlyList<string> Missing,
    string TemplateKey,
    string TemplateName,
    MachineFormHeader Header,
    IReadOnlyList<MachineFormField> Fields,
    IReadOnlyList<MachineFormSection> Checklist,
    IReadOnlyList<string> ChecklistValues,
    IReadOnlyDictionary<string, bool> Answers,
    IReadOnlyDictionary<string, string> Texts,
    IReadOnlyDictionary<string, DateOnly> Dates,
    string? ReviewNote,
    string? CertificateNumber);

public sealed record ChecklistAnswerRequest
{
    /// <summary><c>yes</c>, <c>no</c> or <c>na</c>.</summary>
    [Required, MaxLength(8)]
    public string Value { get; init; } = string.Empty;

    /// <summary>Required when the value is <c>no</c>.</summary>
    [MaxLength(1000)]
    public string? Note { get; init; }
}

/// <summary>What the inspector saves for one machine. Everything is optional: save as often as needed, submit when complete.</summary>
public sealed record SaveMachineFormRequest
{
    /// <summary>Technical fields by key (from <c>fields</c> of the form).</summary>
    public IReadOnlyDictionary<string, string>? Fields { get; init; }

    /// <summary>Checklist answers by <c>section.item</c>.</summary>
    public IReadOnlyDictionary<string, ChecklistAnswerRequest>? Checklist { get; init; }

    /// <summary>
    /// Questionnaire answers: <c>firstExamination</c>, <c>installedCorrectly</c>, <c>within6Months</c>, <c>within12Months</c>,
    /// <c>examinationScheme</c>, <c>exceptionalCircumstances</c>, <c>existingDanger</c>, <c>futureDanger</c>.
    /// </summary>
    public IReadOnlyDictionary<string, bool>? Answers { get; init; }

    /// <summary>
    /// <c>testType</c>, <c>conclusion</c>, <c>defectDescription</c>, <c>repairsRequired</c>, <c>testsCarriedOut</c>, <c>note</c>,
    /// <c>clientRepresentative</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Texts { get; init; }

    /// <summary><c>examination</c>, <c>nextExamination</c>, <c>futureDangerBy</c>, as <c>yyyy-MM-dd</c>.</summary>
    public IReadOnlyDictionary<string, DateOnly>? Dates { get; init; }
}

/// <summary>The technical manager's reason for sending a certificate back.</summary>
public sealed record ReturnMachineRequest
{
    [Required, MaxLength(4000)]
    public string Note { get; init; } = string.Empty;
}

/// <summary>Optional details when the coordinator hands the certificates to the client.</summary>
public sealed record DeliverCertificatesRequest
{
    /// <summary>Who received them (defaults to the client's certificates email).</summary>
    [MaxLength(320)]
    public string? SentTo { get; init; }
}
