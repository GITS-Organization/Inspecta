using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Infrastructure.Repositories;

public sealed class EfJobReportRepository(InspectaDbContext dbContext) : IJobReportRepository
{
    // Ordered in memory: SQLite cannot order by a DateTimeOffset, and a job holds at most a few dozen files.
    public async Task<IReadOnlyList<JobReportFile>> ListForRequestAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        Oldest(await dbContext.JobReportFiles.AsNoTracking().Where(file => file.RequestId == requestId).ToListAsync(cancellationToken));

    public async Task<int> CountForRequestAsync(Guid requestId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var expiries = await dbContext.JobReportFiles.AsNoTracking()
            .Where(file => file.RequestId == requestId)
            .Select(file => file.ExpiresAt)
            .ToListAsync(cancellationToken);
        return expiries.Count(expires => expires is null || expires > now);
    }

    public Task<JobReportFile?> GetWithContentAsync(Guid requestId, Guid fileId, CancellationToken cancellationToken = default) =>
        dbContext.JobReportFiles.AsNoTracking().Include(file => file.Body)
            .SingleOrDefaultAsync(file => file.RequestId == requestId && file.Id == fileId, cancellationToken);

    public async Task<IReadOnlyList<JobReportFile>> ListWithContentAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        Oldest(await dbContext.JobReportFiles.AsNoTracking().Include(file => file.Body)
            .Where(file => file.RequestId == requestId).ToListAsync(cancellationToken));

    public async Task AddAsync(JobReportFile file, CancellationToken cancellationToken = default)
    {
        dbContext.JobReportFiles.Add(file);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // The bytes go with the file: the content row is deleted by the database (ON DELETE CASCADE).
    public async Task<bool> DeleteAsync(Guid requestId, Guid fileId, CancellationToken cancellationToken = default) =>
        await dbContext.JobReportFiles.Where(file => file.RequestId == requestId && file.Id == fileId).ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task StageRetentionAsync(Guid requestId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var files = await dbContext.JobReportFiles.Where(file => file.RequestId == requestId && file.ExpiresAt == null).ToListAsync(cancellationToken);
        foreach (var file in files) file.StartRetention(expiresAt);
    }

    private static IReadOnlyList<JobReportFile> Oldest(List<JobReportFile> files) =>
        files.OrderBy(file => file.CreatedAt.UtcDateTime).ThenBy(file => file.FileName, StringComparer.OrdinalIgnoreCase).ToArray();
}
