namespace Dorosak.Worker;

public sealed partial class WorkerHeartbeatService(
    ILogger<WorkerHeartbeatService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WorkerStarted(logger);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    DateTimeOffset utcTime = timeProvider.GetUtcNow();
                    WorkerHeartbeat(logger, utcTime);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            WorkerStopped(logger);
        }
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Dorosak Worker started")]
    private static partial void WorkerStarted(ILogger logger);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug, Message = "Dorosak Worker heartbeat at {UtcTime}")]
    private static partial void WorkerHeartbeat(ILogger logger, DateTimeOffset utcTime);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Dorosak Worker stopped")]
    private static partial void WorkerStopped(ILogger logger);
}
