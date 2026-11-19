using Inspecta.Application.Contracts.Renewals;
using Inspecta.Application.Contracts.Requests;

namespace Inspecta.Application.Interfaces;

public interface IRenewalsService
{
    /// <param name="window"><c>overdue</c>, <c>30</c>, <c>60</c> or <c>90</c> (default 30).</param>
    Task<RenewalsListResponse> ListAsync(Guid managerId, string? window, string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>The email the client would receive; sends nothing and saves nothing.</summary>
    Task<RenewalReminderPreview> PreviewReminderAsync(Guid managerId, Guid actorId, SendRenewalReminderRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sends the reminder from the actor's own mailbox, once per <c>SendKey</c>.</summary>
    Task<RenewalReminderResult> SendReminderAsync(Guid managerId, Guid actorId, SendRenewalReminderRequest request, CancellationToken cancellationToken = default);

    /// <summary>Creates a normal request for the client with the machines, and links the reminder to it (once).</summary>
    Task<RequestResponse> CreateRequestAsync(Guid managerId, Guid actorId, CreateRenewalRequest request, CancellationToken cancellationToken = default);

    /// <summary>The client said no, or it was handled another way: the reminder ends without a request. Closing again changes nothing.</summary>
    Task<RenewalReminderResponse> CloseReminderAsync(Guid managerId, Guid actorId, Guid reminderId, CancellationToken cancellationToken = default);

    Task<RenewalReminderResponse> GetReminderAsync(Guid managerId, Guid reminderId, CancellationToken cancellationToken = default);

    Task<RenewalReminderPage> ListRemindersAsync(Guid managerId, Guid? clientId, int page, int pageSize, CancellationToken cancellationToken = default);
}
