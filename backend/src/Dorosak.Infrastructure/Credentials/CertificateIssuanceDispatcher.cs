using System.Text.Json;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Credentials;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Operations;
using Dorosak.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Dorosak.Infrastructure.Credentials;

internal sealed class CertificateIssuanceDispatcher(
    DorosakDbContext dbContext,
    ISender sender,
    TimeProvider timeProvider,
    ILogger<CertificateIssuanceDispatcher> logger) : ICertificateIssuanceDispatcher
{
    private const string CourseCompletedEvent = "learning.course-completed";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
    };

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        int processed = 0;
        for (int index = 0; index < 20; index++)
        {
            ClaimedMessage? claimed = await ClaimAsync(cancellationToken);
            if (claimed is null)
            {
                break;
            }

            try
            {
                if (claimed.Message.SchemaVersion != 1)
                {
                    processed += await TerminateAsync(claimed, "CERTIFICATE.DEAD_LETTER.SCHEMA_INVALID", cancellationToken) ? 1 : 0;
                    continue;
                }

                CourseCompleted payload = JsonSerializer.Deserialize<CourseCompleted>(claimed.Message.Payload, JsonOptions)
                    ?? throw new JsonException("Course completion payload is invalid.");
                if (payload.CompletionEnrollmentId == Guid.Empty)
                {
                    throw new JsonException("Course completion payload is invalid.");
                }

                Result<CertificateResponse> result = await sender.Send(
                    new IssueCertificateFromCompletionCommand(payload.CompletionEnrollmentId),
                    cancellationToken);
                if (!result.IsSuccess)
                {
                    throw new InvalidOperationException(result.Failure.Code);
                }

                processed += await OutboxLease.CompleteAsync(
                    dbContext,
                    claimed.Message.Id,
                    claimed.LockToken,
                    timeProvider.GetUtcNow(),
                    logger,
                    cancellationToken) ? 1 : 0;
            }
            catch (JsonException)
            {
                processed += await TerminateAsync(claimed, "CERTIFICATE.DEAD_LETTER.PAYLOAD_INVALID", cancellationToken) ? 1 : 0;
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                if (claimed.AttemptCount >= OutboxLease.MaximumAttempts)
                {
                    processed += await TerminateAsync(claimed, "CERTIFICATE.DEAD_LETTER.MAX_RETRIES", cancellationToken) ? 1 : 0;
                }
                else
                {
                    TimeSpan delay = OutboxLease.GetRetryDelay(claimed.AttemptCount);
                    await OutboxLease.ReleaseAsync(
                        dbContext,
                        claimed.Message.Id,
                        claimed.LockToken,
                        timeProvider.GetUtcNow().Add(delay),
                        exception.GetType().Name,
                        logger,
                        cancellationToken);
                }
            }
        }

        return processed;
    }

    private async Task<ClaimedMessage?> ClaimAsync(CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            OutboxMessage? message = await dbContext.OutboxMessages
                .FromSqlRaw("""
                    SELECT *
                    FROM operations.outbox_messages
                    WHERE processed_at IS NULL
                      AND available_at <= now()
                      AND (locked_until IS NULL OR locked_until <= now())
                      AND event_type = 'learning.course-completed'
                    ORDER BY available_at, occurred_at, id
                    FOR UPDATE SKIP LOCKED
                    LIMIT 1
                    """)
                .SingleOrDefaultAsync(cancellationToken);
            if (message is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            Guid lockToken = Guid.CreateVersion7();
            if (!message.TryAcquire(timeProvider.GetUtcNow(), TimeSpan.FromMinutes(2), lockToken))
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            int attemptCount = message.AttemptCount;
            dbContext.Entry(message).State = EntityState.Detached;
            return new ClaimedMessage(message, lockToken, attemptCount);
        });
    }

    private Task<bool> TerminateAsync(
        ClaimedMessage claimed,
        string code,
        CancellationToken cancellationToken) => OutboxLease.TerminateAsync(
            dbContext,
            claimed.Message.Id,
            claimed.LockToken,
            timeProvider.GetUtcNow(),
            code,
            logger,
            cancellationToken);

    private sealed record CourseCompleted(Guid CompletionEnrollmentId);

    private sealed record ClaimedMessage(OutboxMessage Message, Guid LockToken, int AttemptCount);
}
