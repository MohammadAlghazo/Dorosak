using Dorosak.Domain.Identity;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dorosak.Api.Realtime;

internal sealed class CommunicationsSessionValidationWorker(
    IServiceScopeFactory scopeFactory,
    CommunicationsConnectionRegistry registry,
    IOptions<CommunicationsRealtimeOptions> options,
    TimeProvider timeProvider,
    ILogger<CommunicationsSessionValidationWorker> logger) : BackgroundService
{
    private const int BatchSize = 500;

    private static readonly Action<ILogger, Exception?> IterationFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(5910, nameof(IterationFailed)),
        "Communications session validation iteration failed");

    private static readonly Action<ILogger, string, Exception?> AbortFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(5911, nameof(AbortFailed)),
        "Communications connection {ConnectionId} could not be aborted after session invalidation");

    private readonly TimeSpan _interval = options.Value.SessionValidationInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ValidateOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    IterationFailed(logger, exception);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    internal async Task ValidateOnceAsync(CancellationToken cancellationToken)
    {
        CommunicationsConnectionRegistration[] registrations = registry.Snapshot();
        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (CommunicationsConnectionRegistration[] batch in registrations.Chunk(BatchSize))
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
            Guid[] sessionIds = batch.Select(registration => registration.SessionId).Distinct().ToArray();
            ValidSession[] validSessions = await (
                from session in dbContext.Set<RefreshSession>().AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on session.UserId equals user.Id
                where sessionIds.Contains(session.Id) &&
                    user.IsActive &&
                    user.AuthorizationVersion == session.AuthorizationVersion &&
                    session.RevokedAt == null &&
                    session.IdleExpiresAt > now &&
                    session.AbsoluteExpiresAt > now
                select new ValidSession(session.Id, session.UserId, session.AuthorizationVersion))
                .ToArrayAsync(cancellationToken);
            HashSet<ValidSession> valid = [.. validSessions];

            foreach (CommunicationsConnectionRegistration registration in batch)
            {
                var key = new ValidSession(
                    registration.SessionId,
                    registration.UserId,
                    registration.AuthorizationVersion);
                if (valid.Contains(key) || !registry.Remove(registration))
                {
                    continue;
                }

                try
                {
                    registration.Abort();
                }
                catch (Exception exception)
                {
                    AbortFailed(logger, registration.ConnectionId, exception);
                }
            }
        }
    }

    private sealed record ValidSession(Guid SessionId, Guid UserId, int AuthorizationVersion);
}
