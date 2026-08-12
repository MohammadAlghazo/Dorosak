using Dorosak.Application.Features.Credentials;

namespace Dorosak.Worker;

public sealed class CertificateIssuanceWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<CertificateIssuanceWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> IterationFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(5301, nameof(IterationFailed)),
        "Certificate issuance worker iteration failed");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                ICertificateIssuanceDispatcher dispatcher = scope.ServiceProvider
                    .GetRequiredService<ICertificateIssuanceDispatcher>();
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
