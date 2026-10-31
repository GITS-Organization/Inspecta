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

/// <summary>The inspector's verdict on a machine, printed in the certificate's Conclusion/Remarks.</summary>
public enum InspectionResult
{
    Satisfactory,
    Unsatisfactory
}

public static class InspectionResults
{
    public static CertificateResult ToCertificateResult(this InspectionResult result) =>
        result == InspectionResult.Satisfactory ? CertificateResult.SafeToOperate : CertificateResult.NotSafe;

    public static InspectionResult? ToInspectionResult(this CertificateResult? result) => result switch
    {
        CertificateResult.SafeToOperate or CertificateResult.SafeWithConditions => InspectionResult.Satisfactory,
        CertificateResult.NotSafe => InspectionResult.Unsatisfactory,
        _ => null
    };

    /// <summary>The word printed on the certificate.</summary>
    public static string Label(this InspectionResult result) => result == InspectionResult.Satisfactory ? "Satisfactory" : "Unsatisfactory";
}

/// <summary>
/// One machine of an assigned job, with the state of its certificate. <c>canEdit</c> and <c>canReview</c> tell the current
/// user what they can do with it now. <c>requestItemId</c> is the <c>id</c> of the item in <c>GET /requests/{requestId}</c>.
/// For the review screen it also carries what is printed on the certificate: the machine's <c>type</c>, <c>manufacturer</c>,
/// <c>capacity</c> (SWL), <c>model</c>, the inspection standards and both examination dates, with <c>pdfUrl</c> (the certificate as it
/// will be printed) and <c>qrUrl</c> (what its QR code opens) once the certificate was saved or submitted.
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
    bool CanReview,
    InspectionResult? Result = null,
    string? Type = null,
    string? TemplateName = null,
    string? Manufacturer = null,
    string? Capacity = null,
    string? Model = null,
    IReadOnlyList<string>? InspectionStandards = null,
    DateOnly? ExaminationDate = null,
    DateOnly? NextExaminationDate = null,
    string? PdfUrl = null,
    string? QrUrl = null);

public sealed record MachineProgress(int Total, int Submitted, int Approved, int Delivered);

/// <summary>
/// The machines of one job and how far their certificates are. <c>canStartInspection</c>, <c>canReview</c> (the job waits for the
/// technical manager: show reject), <c>canApprove</c> (it can be approved now: show accept) and <c>canDeliver</c> tell the current
/// user whether those buttons apply to the job now. When <c>canReview</c> is true but <c>canApprove</c> is not, <c>reviewBlockers</c>
/// says why (a machine without a submitted certificate); the job can still be sent back. <c>previewZipUrl</c> downloads every
/// certificate in one ZIP. <c>reviewHistory</c> lists the technical manager's comments each time the job was sent back, newest first.
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
    IReadOnlyList<MachineSummaryResponse> Machines,
    MachineDelivery? Delivery = null,
    bool CanReview = false,
    IReadOnlyList<ReviewCommentResponse>? ReviewHistory = null,
    bool CanApprove = false,
    IReadOnlyList<string>? ReviewBlockers = null,
    string? PreviewZipUrl = null);

/// <summary>One comment of the technical manager: on the whole job (<c>requestItemId</c> empty) or on one machine.</summary>
public sealed record ReviewCommentResponse(DateTimeOffset At, string? By, Guid? RequestItemId, string? MachineName, string Text);

/// <summary>What happened when the certificates were handed over: whether an email went out, to whom, and a note when it did not.</summary>
public sealed record MachineDelivery(bool EmailSent, string? SentTo, string? Notice);

/// <summary>The email that would carry the certificates to the client, for the coordinator to check and edit.</summary>
public sealed record DeliveryPreviewResponse(
    bool EmailEnabled,
    string? To,
    string? ToName,
    string Subject,
    string Body,
    IReadOnlyList<string> Attachments,
    bool RepliesToOriginalEmail,
    string? Notice);

/// <summary>A file to download.</summary>
public sealed record DownloadFile(byte[] Content, string FileName, string ContentType);

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
    string? CertificateNumber,
    InspectionResult? Result = null);

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

    /// <summary><c>satisfactory</c> or <c>unsatisfactory</c>: the one word printed in Conclusion/Remarks. Required before submitting.</summary>
    public InspectionResult? Result { get; init; }
}

/// <summary>Optional details when the technical manager approves a machine.</summary>
public sealed record ApproveMachineRequest
{
    /// <summary>The hand-drawn signature as normalised strokes (a JSON array); printed above the manager's name.</summary>
    public string? Signature { get; init; }

    /// <summary>Keep this signature for the next approvals, so it need not be drawn again.</summary>
    public bool RememberSignature { get; init; }
}

/// <summary>The technical manager's reason for sending a certificate back.</summary>
public sealed record ReturnMachineRequest
{
    [Required, MaxLength(4000)]
    public string Note { get; init; } = string.Empty;
}

/// <summary>The technical manager sends the whole job back to the inspector.</summary>
public sealed record ReturnJobRequest
{
    /// <summary>What to fix in general; shown on every machine and kept in the job's review history.</summary>
    [Required, MaxLength(4000)]
    public string Note { get; init; } = string.Empty;

    /// <summary>Optional comments on single machines (a wrong serial number, a spelling mistake...).</summary>
    public IReadOnlyList<MachineReviewComment>? Machines { get; init; }
}

/// <summary>A comment on one machine when the job is sent back.</summary>
public sealed record MachineReviewComment
{
    [Required]
    public Guid RequestItemId { get; init; }

    [Required, MaxLength(4000)]
    public string Comment { get; init; } = string.Empty;
}

/// <summary>Optional details when the coordinator hands the certificates to the client.</summary>
public sealed record DeliverCertificatesRequest
{
    /// <summary>Who received them (defaults to the client's certificates email).</summary>
    [MaxLength(320)]
    public string? SentTo { get; init; }

    /// <summary>The text of the email to the client; leave out to use the suggested text from the preview.</summary>
    [MaxLength(8000)]
    public string? Message { get; init; }
}
