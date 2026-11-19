using Inspecta.Application.Contracts.Renewals;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public interface IRenewalsRepository
{
    /// <summary>
    /// The clients of the company that have an active, not archived machine overdue or due before <paramref name="dueBefore"/>, with those machines, their
    /// contacts that have an email, and whether each machine is on an open request. Retired and archived machines and archived clients are left out.
    /// </summary>
    Task<IReadOnlyList<RenewalClientRows>> ListDueAsync(Guid managerId, DateTimeOffset dueBefore, CancellationToken cancellationToken = default);

    /// <summary>The client with its saved addresses, or <c>null</c> when it is archived or not of this company.</summary>
    Task<RenewalClientInfo?> GetClientAsync(Guid managerId, Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>The machines of the company with these ids (an id that is not found is left out).</summary>
    Task<IReadOnlyList<RenewalMachineInfo>> GetMachinesAsync(Guid managerId, IReadOnlyList<Guid> equipmentIds, CancellationToken cancellationToken = default);

    Task<RenewalReminder?> GetReminderBySendKeyAsync(Guid managerId, Guid sendKey, CancellationToken cancellationToken = default);
    Task<RenewalReminder?> GetReminderAsync(Guid managerId, Guid reminderId, CancellationToken cancellationToken = default);

    /// <summary>Adds the reminder. <c>false</c> when a reminder with the same key exists already (two clicks at the same moment): nothing was added.</summary>
    Task<bool> TryAddReminderAsync(RenewalReminder reminder, CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the changes. <c>false</c> when another click changed the reminder first (the retry of a failed reminder, two clicks at the same moment): nothing was
    /// saved and the caller does not send.
    /// </summary>
    Task<bool> TrySaveAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads the reminder again, after a conditional update changed it behind the tracked copy.</summary>
    Task ReloadReminderAsync(RenewalReminder reminder, CancellationToken cancellationToken = default);

    /// <summary>Closes the reminder only if it has no request and is not closed yet. <c>false</c> when it was claimed or closed meanwhile.</summary>
    Task<bool> CloseReminderAsync(Guid managerId, Guid reminderId, Guid actorId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links the reminder to the request it became, only if it has none and is not closed. <c>false</c> when it was claimed meanwhile (two clicks at the same
    /// moment): the caller then throws, and the transaction it runs in undoes the request it just made.
    /// </summary>
    Task<bool> ClaimReminderAsync(Guid managerId, Guid reminderId, Guid requestId, Guid actorId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>The reminders newest first, of one client or all.</summary>
    Task<(IReadOnlyList<RenewalReminder> Items, int Total)> ListRemindersAsync(Guid managerId, Guid? clientId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The newest reminder of each of the clients.</summary>
    Task<IReadOnlyDictionary<Guid, RenewalReminder>> LatestByClientAsync(Guid managerId, IReadOnlyCollection<Guid> clientIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RenewalReminderReply>> RepliesAsync(IReadOnlyCollection<Guid> reminderIds, CancellationToken cancellationToken = default);

    // ---- the mailbox import has no signed-in user: these are limited by the company id, never by the query filters.

    /// <summary>
    /// The reminder that one of the message ids (lowercase, no angle brackets) belongs to, whatever its state; ready to be changed. The id is a random value made
    /// by us: a match is a reply to our email even if the mail server's answer was lost and the row says failed.
    /// </summary>
    Task<RenewalReminder?> FindReminderByMessageIdAsync(Guid managerId, IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default);

    /// <summary>Whether the message id is one of the reminders Inspecta sent.</summary>
    Task<bool> IsOwnReminderMessageAsync(Guid managerId, string messageId, CancellationToken cancellationToken = default);

    Task<bool> ReplyExistsAsync(Guid managerId, string messageId, CancellationToken cancellationToken = default);

    /// <summary>Saves the reply and the reminder it was recorded on in one go.</summary>
    Task AddReplyAsync(RenewalReminderReply reply, RenewalReminder reminder, CancellationToken cancellationToken = default);
}
