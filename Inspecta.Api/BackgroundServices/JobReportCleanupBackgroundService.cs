using Inspecta.Application.Interfaces;

namespace Inspecta.Api.BackgroundServices;

/// <summary>
/// Deletes the report files of delivered jobs once their retention (<c>JobReports:RetentionDays</c> after delivery, stamped on each file) has run
/// out: first run a minute after start-up, then every hour. Expired files are already treated as gone everywhere before this frees the space.
/// </summary>
public sealed class JobReportCleanupBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<JobReportCleanupBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>How many files one delete statement removes, so a run never holds one huge transaction.</summary>
    public const int BatchSize = 200;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>One pass; errors are logged and never escape, so the service keeps running. Returns how many files were deleted.</summary>
    public async Task<int> CleanAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IJobReportRepository>();
            var deleted = await repository.DeleteExpiredAsync(DateTimeOffset.UtcNow, BatchSize, cancellationToken);
            if (deleted > 0)
                logger.LogInformation("Deleted {Count} expired job report files.", deleted);
            return deleted;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not delete expired job report files.");
            return 0;
        }
    }
}
