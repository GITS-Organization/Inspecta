using Inspecta.Application.Interfaces;

namespace Inspecta.Infrastructure.Data;

public sealed class EfTransactionRunner(InspectaDbContext dbContext) : ITransactionRunner
{
    public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default)
    {
        // Already inside a transaction (a caller wrapped the work): join it.
        if (dbContext.Database.CurrentTransaction is not null) return await work();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await work();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
