using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfOutboundEmailRepository(InspectaDbContext dbContext) : IOutboundEmailRepository
{
    private const string QuoteKindIndexName = "IX_OutboundEmails_QuoteId_Kind";

    public Task<OutboundEmail?> GetAsync(Guid quoteId, OutboundEmailKind kind, CancellationToken cancellationToken = default) =>
        dbContext.OutboundEmails.SingleOrDefaultAsync(email => email.QuoteId == quoteId && email.Kind == kind, cancellationToken);

    // The attachment is not loaded: lists only need the delivery state.
    public async Task<IReadOnlyList<OutboundEmailSummary>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        await dbContext.OutboundEmails.AsNoTracking()
            .Where(email => email.RequestId == requestId)
            .OrderBy(email => email.CreatedAt)
            .Select(email => new OutboundEmailSummary(
                email.QuoteId, email.Kind, email.Status, email.ToAddress, email.Attempts, email.SentAt, email.LastError, email.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(OutboundEmail email, CancellationToken cancellationToken = default)
    {
        dbContext.OutboundEmails.Add(email);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: QuoteKindIndexName
            })
        {
            dbContext.Entry(email).State = EntityState.Detached;
            throw new InvalidOperationException("This email is already being sent.");
        }
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
