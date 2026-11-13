using Inspecta.Domain.Enums;

namespace Inspecta.Application.Executive;

// Plain rows of one company, read once per answer and turned into figures by the calculators. They carry names, numbers, dates and statuses
// only: nothing a request or a quotation says is read into them.

public sealed record ExecPayment(DateOnly PaidOn, long AmountPiastres, DateTimeOffset RecordedAt, Guid? ReversesPaymentId);

public sealed record ExecInvoiceEvent(InvoiceEventKind Kind, DateTimeOffset At, Guid? ActorId);

public sealed record ExecInvoice(
    Guid Id,
    string Number,
    Guid ManagerId,
    Guid RequestId,
    Guid? ClientId,
    string ClientName,
    long TotalPiastres,
    DateTimeOffset CreatedAt,
    InvoiceStage Stage,
    bool ClosedWithBalance,
    IReadOnlyList<ExecPayment> Payments,
    IReadOnlyList<ExecInvoiceEvent> Events)
{
    public long PaidPiastres => Payments.Sum(item => item.AmountPiastres);
    public long BalancePiastres => TotalPiastres - PaidPiastres;
}

public sealed record ExecTransition(JobStatus? From, JobStatus To, DateTimeOffset At, Guid ActorId);

public sealed record ExecRequest(
    Guid Id,
    Guid ManagerId,
    Guid? ClientId,
    string ClientName,
    RequestKind Kind,
    RequestTriage Triage,
    JobStatus Status,
    Guid? ReviewerId,
    Guid? CoordinatorId,
    Guid? InspectorId,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ScheduledAt,
    int MachineCount,
    IReadOnlyList<Guid> EquipmentIds,
    IReadOnlyList<ExecTransition> Transitions)
{
    /// <summary>The short reference the screens show: REQ- and the first six characters of the id.</summary>
    public string Number => "REQ-" + Id.ToString("N")[..6].ToUpperInvariant();
}

public sealed record ExecQuote(
    Guid Id,
    Guid RequestId,
    int Version,
    QuoteStatus Status,
    DateTimeOffset? SentAt,
    DateTimeOffset? ClientRespondedAt,
    DateTimeOffset? SupersededAt,
    DateTimeOffset ValidUntil,
    bool IsPlaceholder,
    long TotalWithVatPiastres,
    long? ClientCounterPricePiastres);

public sealed record ExecCertificate(
    Guid RequestId,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    CertificateResult? Result,
    Guid? ReviewerId,
    Guid InspectorId);

public sealed record ExecMachine(
    Guid Id,
    string Name,
    string? Serial,
    string? Capacity,
    Guid ClientId,
    CertificateResult? LastResult,
    DateTimeOffset? NextDueAt);

public sealed record ExecContact(string Name, string? Role, string? Email, string? Phone);

public sealed record ExecClient(
    Guid Id,
    string Name,
    string? Location,
    bool HasContract,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<ExecContact> Contacts);

public sealed record ExecPerson(Guid Id, string Name, UserRole Role, bool IsActive, DateTimeOffset? OnLeaveUntil);

/// <summary>Everything one company has, as rows. Machines are only the active, not archived ones of clients that are not archived, with a next inspection date.</summary>
public sealed record ExecutiveData(
    IReadOnlyList<ExecInvoice> Invoices,
    IReadOnlyList<ExecRequest> Requests,
    IReadOnlyList<ExecQuote> Quotes,
    IReadOnlyList<ExecCertificate> Certificates,
    IReadOnlyList<ExecMachine> Machines,
    IReadOnlyList<ExecClient> Clients,
    IReadOnlyList<ExecPerson> People);
