using Dorosak.Application.Features.Communications;
using Microsoft.Extensions.Options;

namespace Dorosak.Api.Realtime;

internal sealed class CommunicationsRealtimeWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<CommunicationsRealtimeOptions> options,
    ILogger<CommunicationsRealtimeWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> IterationFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(5901, nameof(IterationFailed)),
        "Communications realtime worker iteration failed");

    private readonly CommunicationsRealtimeOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                ICommunicationsRealtimeDispatcher dispatcher = scope.ServiceProvider
                    .GetRequiredService<ICommunicationsRealtimeDispatcher>();
                int processed = await dispatcher.DispatchPendingAsync(stoppingToken);
                if (processed == 0)
                {
                    await Task.Delay(_options.IdleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                IterationFailed(logger, exception);
                await Task.Delay(_options.FailureDelay, stoppingToken);
            }
        }
    }
}
