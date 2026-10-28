using Inspecta.Application.Interfaces;
using Inspecta.Domain.Entities;

namespace Inspecta.Api.BackgroundServices;

/// <summary>Deletes "maybe" emails the coordinator did not confirm within <see cref="InspectionRequest.MaybeRetention"/>.</summary>
public sealed class MaybeCleanupBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<MaybeCleanupBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

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

    private async Task CleanAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IEmailSyncRepository>();
            var deleted = await repository.DeleteExpiredMaybeRequestsAsync(
                DateTimeOffset.UtcNow - InspectionRequest.MaybeRetention,
                cancellationToken);
            if (deleted > 0)
                logger.LogInformation("Deleted {Count} expired maybe emails.", deleted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not delete expired maybe emails.");
        }
    }
}
