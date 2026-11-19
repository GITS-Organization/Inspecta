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

/// <summary>What the coordinator sends: the client, the machines (ticked), who gets it and the message. <c>SendKey</c> is made by the screen when the reminder is opened.</summary>
public sealed record SendRenewalReminderRequest(Guid? SendKey, Guid ClientId, IReadOnlyList<Guid> EquipmentIds, string ToAddress, string Message, string? Subject = null);

/// <summary>The email as the client will see it. <c>Blockers</c> say why Send would not work (mailbox not configured, emailing switched off).</summary>
public sealed record RenewalReminderPreview(string Subject, string? FromAddress, string? FromName, string ToAddress, string Html, string Text, IReadOnlyList<string> Blockers);

public sealed record RenewalReminderUser(Guid Id, string Name);

public sealed record RenewalReminderMachine(Guid EquipmentId, string Name, string? SerialNumber, DateOnly? LastInspectedOn, DateOnly NextDueOn);

/// <summary>An answer of the client, as text only: the client's HTML is never returned.</summary>
public sealed record RenewalReminderReplyItem(Guid Id, string? FromAddress, string? FromName, DateTimeOffset ReceivedAt, string? Subject, string Text);

public sealed record RenewalReminderResponse(
    Guid Id, Guid ClientId, string State, RenewalReminderUser SentBy, DateTimeOffset CreatedAt, DateTimeOffset? SentAt, int Attempts, string? LastError,
    string ToAddress, string? ToName, string FromAddress, string Subject, string Message,
    IReadOnlyList<RenewalReminderMachine> Machines, IReadOnlyList<RenewalReminderReplyItem> Replies,
    Guid? RequestId, DateTimeOffset? RequestCreatedAt, DateTimeOffset? ClosedAt);

/// <summary><c>Created</c> is false when the same <c>sendKey</c> was already sent: nothing was sent again.</summary>
public sealed record RenewalReminderResult(RenewalReminderResponse Reminder, bool Created);

public sealed record RenewalReminderPage(int Total, IReadOnlyList<RenewalReminderResponse> Items);

/// <summary>A client of the company with its saved addresses: a reminder goes to one of them only.</summary>
public sealed record RenewalClientInfo(Guid Id, string Name, string? Location, string? CertificatesEmail, IReadOnlyList<RenewalContactRow> Contacts);

/// <summary>A machine as the reminder check needs it.</summary>
public sealed record RenewalMachineInfo(
    Guid Id, Guid ClientId, string Name, string? SerialNumber, string? Capacity, bool Retired, bool Archived,
    DateTimeOffset? NextDueAt, DateTimeOffset? LastInspectionAt, DateOnly? LastInspectedOn);

/// <summary>What the repository hands the service: the rows only, no windows or days.</summary>
public sealed record RenewalClientRows(
    Guid ClientId, string Name, string? Location, string? CertificatesEmail,
    IReadOnlyList<RenewalMachineRow> Machines, IReadOnlyList<RenewalContactRow> Contacts);

public sealed record RenewalMachineRow(
    Guid EquipmentId, string Name, string? SerialNumber, string? Capacity, DateTimeOffset? LastInspectionAt, DateOnly? LastInspectedOn,
    DateTimeOffset NextDueAt, bool OnOpenRequest);

public sealed record RenewalContactRow(Guid Id, string Name, string? Role, string Email);
