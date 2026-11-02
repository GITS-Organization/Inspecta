using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Application.Services;

public sealed class ClientReplyService(IClientReplyRepository repository, TimeProvider? clock = null) : IClientReplyService
{
    public async Task<ClientReplyOutcome> TryAttachAsync(
        IncomingEmail email,
        Guid managerId,
        Guid coordinatorId,
        IReadOnlyList<string> ownAddresses,
        CancellationToken cancellationToken = default)
    {
        // Only an email that says what it answers can be one: a new request has neither header.
        var ids = MessageIds(email.InReplyTo, email.References);
        if (ids.Count == 0) return ClientReplyOutcome.NotAReply;

        var target = await repository.FindTargetAsync(managerId, ids, cancellationToken);
        if (target is null) return ClientReplyOutcome.NotAReply;

        // It does answer one of our emails, so it must not turn into a request. But it is kept only when it is a real answer.
        var own = MessageId(email.MessageId) is { } id && await repository.IsOwnEmailAsync(managerId, id, cancellationToken);
        var fromTheMailbox = email.FromAddress is { Length: > 0 } sender &&
                             ownAddresses.Any(address => string.Equals(address, sender, StringComparison.OrdinalIgnoreCase));
        if (own || fromTheMailbox || email.IsAutomatic) return ClientReplyOutcome.Ignored;

        // Imported twice (a retry after a failure): kept once.
        if (MessageId(email.MessageId) is { } messageId && await repository.ExistsAsync(managerId, messageId, cancellationToken))
            return ClientReplyOutcome.Attached;

        var request = await repository.GetRequestAsync(managerId, target.RequestId, cancellationToken);
        if (request is null) return ClientReplyOutcome.NotAReply;

        var reply = ClientReply.Create(managerId, request.Id, target.QuoteId, email, (clock ?? TimeProvider.System).GetUtcNow());
        request.ReceiveClientReply(coordinatorId);
        await repository.AddAsync(reply, request, cancellationToken);
        return ClientReplyOutcome.Attached;
    }

    /// <summary>The message ids of In-Reply-To and References, lowercase and without angle brackets, once each.</summary>
    internal static IReadOnlyList<string> MessageIds(string? inReplyTo, string? references) =>
        $"{inReplyTo} {references}"
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(MessageId)
            .Where(id => id is not null)
            .Select(id => id!)
            .Distinct()
            .ToArray();

    internal static string? MessageId(string? value)
    {
        var id = value?.Trim().Trim('<', '>').Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(id) ? null : id;
    }
}
