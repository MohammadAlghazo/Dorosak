using Dorosak.Infrastructure.Identity;

namespace Dorosak.Worker;

public sealed class IdentityEmailWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<IdentityEmailWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> IterationFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(5201, nameof(IterationFailed)),
        "Identity email worker iteration failed");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IIdentityEmailDispatcher dispatcher = scope.ServiceProvider
                    .GetRequiredService<IIdentityEmailDispatcher>();
                int processed = await dispatcher.DispatchPendingAsync(stoppingToken);
                if (processed == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                IterationFailed(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
