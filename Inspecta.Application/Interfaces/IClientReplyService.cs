using Inspecta.Domain.Entities;

namespace Inspecta.Application.Interfaces;

public enum ClientReplyOutcome
{
    /// <summary>The email does not answer one of our quotation emails: it is imported as usual.</summary>
    NotAReply,

    /// <summary>It answers a quotation: it was kept with the request and is not imported as a new request.</summary>
    Attached,

    /// <summary>It answers a quotation but is no answer (an automatic reply, or our own email): it is dropped.</summary>
    Ignored
}

/// <summary>Recognises the emails that answer a quotation we sent, so they do not become new requests.</summary>
public interface IClientReplyService
{
    /// <param name="managerId">The team of the mailbox.</param>
    /// <param name="coordinatorId">The owner of the mailbox: the one who gets the request back.</param>
    /// <param name="ownAddresses">The addresses of the mailbox itself: an email from them is ours, not the client's.</param>
    Task<ClientReplyOutcome> TryAttachAsync(
        IncomingEmail email,
        Guid managerId,
        Guid coordinatorId,
        IReadOnlyList<string> ownAddresses,
        CancellationToken cancellationToken = default);
}
