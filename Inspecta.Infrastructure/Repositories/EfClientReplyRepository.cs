using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

/// <summary>
/// The replies of clients to the quotations. The mailbox import has no signed-in user, so what it reads is always limited by the team id,
/// never by the query filters.
/// </summary>
public sealed class EfClientReplyRepository(InspectaDbContext dbContext) : IClientReplyRepository
{
    public async Task<ReplyTarget?> FindTargetAsync(Guid managerId, IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default)
    {
        if (messageIds.Count == 0) return null;
        var ids = messageIds.ToArray();

        // The email we sent: the most specific answer. Only a few can match, so the newest is picked here.
        var outbound = (await dbContext.OutboundEmails.IgnoreQueryFilters().AsNoTracking()
                .Where(email => email.ManagerId == managerId && ids.Contains(email.MessageId.ToLower()))
                .Select(email => new { email.RequestId, email.QuoteId, email.CreatedAt })
                .ToListAsync(cancellationToken))
            .OrderByDescending(email => email.CreatedAt)
            .FirstOrDefault();
        if (outbound is not null) return new ReplyTarget(outbound.RequestId, outbound.QuoteId);

        // An answer in the thread of the original email counts once a quotation was sent for the request.
        var requestId = await dbContext.RequestEmails.IgnoreQueryFilters().AsNoTracking()
            .Where(email => email.MessageId != null && ids.Contains(email.MessageId.ToLower()) && email.Request!.ManagerId == managerId)
            .Select(email => (Guid?)email.RequestId)
            .FirstOrDefaultAsync(cancellationToken);
        if (requestId is Guid id &&
            await dbContext.OutboundEmails.IgnoreQueryFilters().AnyAsync(
                email => email.RequestId == id && email.Status == OutboundEmailStatus.Sent, cancellationToken))
            return new ReplyTarget(id, null);

        return null;
    }

    public Task<bool> IsOwnEmailAsync(Guid managerId, string messageId, CancellationToken cancellationToken = default) =>
        dbContext.OutboundEmails.IgnoreQueryFilters()
            .AnyAsync(email => email.ManagerId == managerId && email.MessageId.ToLower() == messageId, cancellationToken);

    public Task<bool> ExistsAsync(Guid managerId, string messageId, CancellationToken cancellationToken = default) =>
        dbContext.ClientReplies.IgnoreQueryFilters()
            .AnyAsync(reply => reply.ManagerId == managerId && reply.MessageId != null && reply.MessageId.ToLower() == messageId, cancellationToken);

    public Task<InspectionRequest?> GetRequestAsync(Guid managerId, Guid requestId, CancellationToken cancellationToken = default) =>
        dbContext.Requests.IgnoreQueryFilters()
            .Include(request => request.History)
            .SingleOrDefaultAsync(request => request.Id == requestId && request.ManagerId == managerId, cancellationToken);

    public async Task AddAsync(ClientReply reply, InspectionRequest request, CancellationToken cancellationToken = default)
    {
        dbContext.ClientReplies.Add(reply);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClientReply>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        (await dbContext.ClientReplies.AsNoTracking().Where(reply => reply.RequestId == requestId).ToListAsync(cancellationToken))
            .OrderBy(reply => reply.ReceivedAt)
            .ToArray();

    public async Task RemoveForRequestAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        dbContext.ClientReplies.RemoveRange(await dbContext.ClientReplies.Where(reply => reply.RequestId == requestId).ToListAsync(cancellationToken));
}
