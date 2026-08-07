using Dorosak.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace Dorosak.Worker;

public sealed class AdminBootstrapWorker(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime applicationLifetime,
    IOptions<AdminBootstrapOptions> options,
    ILogger<AdminBootstrapWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> BootstrapFailed = LoggerMessage.Define(
        LogLevel.Critical,
        new EventId(5202, nameof(BootstrapFailed)),
        "The one-time administrator bootstrap failed");

    private static readonly Action<ILogger, bool, bool, Exception?> BootstrapCompleted =
        LoggerMessage.Define<bool, bool>(
            LogLevel.Information,
            new EventId(5203, nameof(BootstrapCompleted)),
            "Administrator bootstrap completed. Created={Created}, AlreadyExists={AlreadyExists}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IAdminBootstrapService bootstrap = scope.ServiceProvider.GetRequiredService<IAdminBootstrapService>();
            AdminBootstrapResult result = await bootstrap.ExecuteAsync(stoppingToken);
            BootstrapCompleted(logger, result.Created, result.AlreadyExists, null);
        }
        catch (Exception exception)
        {
            BootstrapFailed(logger, exception);
            Environment.ExitCode = 1;
        }
        finally
        {
            applicationLifetime.StopApplication();
        }
    }
}
