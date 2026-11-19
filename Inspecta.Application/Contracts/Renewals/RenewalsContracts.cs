namespace Inspecta.Application.Contracts.Renewals;

/// <summary>The Renewals list: the machines that are due, grouped by client. <c>Counts</c> include the overdue machines (a machine due in 30 days or already late counts in <c>Within30</c>).</summary>
public sealed record RenewalsListResponse(
    DateOnly Today, RenewalCounts Counts, int Total, int Page, int PageSize, IReadOnlyList<RenewalClient> Clients);

public sealed record RenewalCounts(int Overdue, int Within30, int Within60, int Within90);

/// <summary>
/// A client with machines due. <c>Machines</c> holds every active machine of the client that is overdue or due within 90 days; <c>InWindow</c> says which ones belong
/// to the window asked for (the screen ticks those). <c>Recipients</c> are the only addresses a reminder may go to.
/// </summary>
public sealed record RenewalClient(
    Guid ClientId, string ClientName, string? Location, int DueInWindow, int LateInWindow,
    IReadOnlyList<RenewalMachine> Machines, IReadOnlyList<RenewalRecipient> Recipients, RenewalReminderSummary? LatestReminder);

public sealed record RenewalMachine(
    Guid EquipmentId, string Name, string? SerialNumber, string? Capacity, DateOnly? LastInspectedOn, DateOnly NextDueOn,
    int DaysUntilDue, bool InWindow, bool OnOpenRequest);

/// <summary><c>Kind</c> is <c>certificates_email</c> (the client's certificates address) or <c>contact</c>.</summary>
public sealed record RenewalRecipient(string Email, string Kind, Guid? ContactId, string? Name, string? Role);

/// <summary>The newest reminder sent to the client (<c>State</c>: sending, failed, sent, replied, requested or closed).</summary>
public sealed record RenewalReminderSummary(Guid Id, string State, DateTimeOffset? SentAt, string? ToAddress, int MachineCount, DateTimeOffset? LastReplyAt);

/// <summary>What the repository hands the service: the rows only, no windows or days.</summary>
public sealed record RenewalClientRows(
    Guid ClientId, string Name, string? Location, string? CertificatesEmail,
    IReadOnlyList<RenewalMachineRow> Machines, IReadOnlyList<RenewalContactRow> Contacts);

public sealed record RenewalMachineRow(
    Guid EquipmentId, string Name, string? SerialNumber, string? Capacity, DateTimeOffset? LastInspectionAt, DateOnly? LastInspectedOn,
    DateTimeOffset NextDueAt, bool OnOpenRequest);

public sealed record RenewalContactRow(Guid Id, string Name, string? Role, string Email);
