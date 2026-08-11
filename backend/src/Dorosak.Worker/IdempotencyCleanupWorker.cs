using Dorosak.Application.Common.Idempotency;

namespace Dorosak.Worker;

public sealed partial class IdempotencyCleanupWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<IdempotencyCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        do
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IIdempotencyRecordCleaner cleaner = scope.ServiceProvider
                    .GetRequiredService<IIdempotencyRecordCleaner>();
                int deletedCount = await cleaner.DeleteExpiredAsync(
                    timeProvider.GetUtcNow(),
                    stoppingToken);
                if (deletedCount > 0)
                {
                    RecordsDeleted(logger, deletedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                CleanupFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(EventId = 5300, Level = LogLevel.Information, Message = "Deleted {DeletedCount} expired idempotency records")]
    private static partial void RecordsDeleted(ILogger logger, int deletedCount);

    [LoggerMessage(EventId = 5301, Level = LogLevel.Error, Message = "Idempotency cleanup iteration failed")]
    private static partial void CleanupFailed(ILogger logger, Exception exception);
}
