using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dorosak.Infrastructure.Operations;

internal static class OutboxLease
{
    public const int MaximumAttempts = 8;

    private const int MaximumErrorCodeLength = 200;

    private static readonly Action<ILogger, Guid, string, Exception?> LeaseLost =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Warning,
            new EventId(5100, nameof(LeaseLost)),
            "Outbox message {MessageId} lease was lost before {Operation}");

    public static Task<bool> CompleteAsync(
        DorosakDbContext dbContext,
        Guid messageId,
        Guid lockToken,
        DateTimeOffset processedAt,
        ILogger logger,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            messageId,
            "completion",
            logger,
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE operations.outbox_messages
                SET processed_at = {processedAt},
                    locked_until = NULL,
                    lock_token = NULL,
                    last_error_code = NULL
                WHERE id = {messageId}
                  AND lock_token = {lockToken}
                  AND processed_at IS NULL
                """,
                cancellationToken));

    public static Task<bool> ReleaseAsync(
        DorosakDbContext dbContext,
        Guid messageId,
        Guid lockToken,
        DateTimeOffset availableAt,
        string errorCode,
        ILogger logger,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            messageId,
            "retry release",
            logger,
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE operations.outbox_messages
                SET available_at = {availableAt},
                    locked_until = NULL,
                    lock_token = NULL,
                    last_error_code = {Bound(errorCode)}
                WHERE id = {messageId}
                  AND lock_token = {lockToken}
                  AND processed_at IS NULL
                """,
                cancellationToken));

    public static Task<bool> TerminateAsync(
        DorosakDbContext dbContext,
        Guid messageId,
        Guid lockToken,
        DateTimeOffset processedAt,
        string deadLetterCode,
        ILogger logger,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            messageId,
            "terminal handling",
            logger,
            dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE operations.outbox_messages
                SET processed_at = {processedAt},
                    locked_until = NULL,
                    lock_token = NULL,
                    last_error_code = {Bound(deadLetterCode)}
                WHERE id = {messageId}
                  AND lock_token = {lockToken}
                  AND processed_at IS NULL
                """,
                cancellationToken));

    public static TimeSpan GetRetryDelay(int attemptCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attemptCount);
        double exponentialSeconds = Math.Min(300, Math.Pow(2, Math.Min(attemptCount, MaximumAttempts)));
        double jitter = Random.Shared.NextDouble() * 0.2;
        return TimeSpan.FromSeconds(exponentialSeconds * (1 + jitter));
    }

    private static async Task<bool> ExecuteAsync(
        Guid messageId,
        string operation,
        ILogger logger,
        Task<int> update)
    {
        int affectedRows = await update;
        if (affectedRows == 0)
        {
            LeaseLost(logger, messageId, operation, null);
            return false;
        }

        return true;
    }

    private static string Bound(string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        return errorCode.Length <= MaximumErrorCodeLength
            ? errorCode
            : errorCode[..MaximumErrorCodeLength];
    }
}
