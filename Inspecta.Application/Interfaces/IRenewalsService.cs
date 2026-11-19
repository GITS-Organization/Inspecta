using Inspecta.Application.Contracts.Renewals;

namespace Inspecta.Application.Interfaces;

public interface IRenewalsService
{
    /// <param name="window"><c>overdue</c>, <c>30</c>, <c>60</c> or <c>90</c> (default 30).</param>
    Task<RenewalsListResponse> ListAsync(Guid managerId, string? window, string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The email the client would receive; sends nothing and saves nothing.</summary>
    Task<RenewalReminderPreview> PreviewReminderAsync(Guid managerId, Guid actorId, SendRenewalReminderRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sends the reminder from the actor's own mailbox, once per <c>SendKey</c>.</summary>
    Task<RenewalReminderResult> SendReminderAsync(Guid managerId, Guid actorId, SendRenewalReminderRequest request, CancellationToken cancellationToken = default);

    Task<RenewalReminderResponse> GetReminderAsync(Guid managerId, Guid reminderId, CancellationToken cancellationToken = default);

    Task<RenewalReminderPage> ListRemindersAsync(Guid managerId, Guid? clientId, int page, int pageSize, CancellationToken cancellationToken = default);
}
