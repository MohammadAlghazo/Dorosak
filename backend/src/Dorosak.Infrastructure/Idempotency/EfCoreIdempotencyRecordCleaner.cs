using Dorosak.Application.Common.Idempotency;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Idempotency;

internal sealed class EfCoreIdempotencyRecordCleaner(DorosakDbContext dbContext)
    : IIdempotencyRecordCleaner
{
    public Task<int> DeleteExpiredAsync(
        DateTimeOffset expiresAtOrBefore,
        CancellationToken cancellationToken) =>
        dbContext.IdempotencyRecords
            .Where(record => record.ExpiresAt <= expiresAtOrBefore)
            .ExecuteDeleteAsync(cancellationToken);
}
