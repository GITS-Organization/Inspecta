using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

/// <summary>The request (and the quotation, when the email says which) a reply from the client answers.</summary>
public sealed record ReplyTarget(Guid RequestId, Guid? QuoteId);

public interface IClientReplyRepository
{
    /// <summary>
    /// The quotation email we sent (or, once a quotation was sent for the request, the original email of the request) that one of the
    /// message ids belongs to; <c>null</c> when none does. The ids are lowercase, without angle brackets.
    /// </summary>
    Task<ReplyTarget?> FindTargetAsync(Guid managerId, IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default);

    /// <summary>Whether the message id is one of the emails Inspecta sent.</summary>
    Task<bool> IsOwnEmailAsync(Guid managerId, string messageId, CancellationToken cancellationToken = default);

    /// <summary>Whether a reply with this message id was already kept.</summary>
    Task<bool> ExistsAsync(Guid managerId, string messageId, CancellationToken cancellationToken = default);

    /// <summary>The request, ready to be changed, whoever asks (the mailbox import has no signed-in user).</summary>
    Task<InspectionRequest?> GetRequestAsync(Guid managerId, Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Saves the reply and the changes made to the request in one go.</summary>
    Task AddAsync(ClientReply reply, InspectionRequest request, CancellationToken cancellationToken = default);

    /// <summary>The replies of the request, oldest first.</summary>
    Task<IReadOnlyList<ClientReply>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Removes the replies of the request. They go with the next save of the quotation.</summary>
    Task RemoveForRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
}
