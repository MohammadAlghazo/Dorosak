namespace Dorosak.Application.Common.Idempotency;

public interface IIdempotencyRecordCleaner
{
    Task<int> DeleteExpiredAsync(DateTimeOffset expiresAtOrBefore, CancellationToken cancellationToken);
}
