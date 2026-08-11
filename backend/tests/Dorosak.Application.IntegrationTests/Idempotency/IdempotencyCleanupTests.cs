using Dorosak.Application.Common.Idempotency;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dorosak.Application.IntegrationTests.Idempotency;

[Collection(InfrastructureTestGroup.Name)]
public sealed class IdempotencyCleanupTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task CleanerDeletesExpiredRecordsAndKeepsActiveRecords()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        IdempotencyRecord expired = CreateRecord(
            Guid.CreateVersion7().ToString("N"),
            now.AddHours(-2),
            now.AddHours(-1));
        IdempotencyRecord active = CreateRecord(
            Guid.CreateVersion7().ToString("N"),
            now.AddMinutes(-1),
            now.AddHours(1));

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        DorosakDbContext dbContext = scope.ServiceProvider.GetRequiredService<DorosakDbContext>();
        dbContext.Set<IdempotencyRecord>().AddRange(expired, active);
        await dbContext.SaveChangesAsync(cancellationToken);

        int deleted = await scope.ServiceProvider.GetRequiredService<IIdempotencyRecordCleaner>()
            .DeleteExpiredAsync(now, cancellationToken);

        Assert.Equal(1, deleted);
        Assert.False(await dbContext.Set<IdempotencyRecord>().AsNoTracking()
            .AnyAsync(record => record.Id == expired.Id, cancellationToken));
        Assert.True(await dbContext.Set<IdempotencyRecord>().AsNoTracking()
            .AnyAsync(record => record.Id == active.Id, cancellationToken));
        await dbContext.Set<IdempotencyRecord>()
            .Where(record => record.Id == active.Id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static IdempotencyRecord CreateRecord(
        string key,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt) =>
        IdempotencyRecord.CreateCompleted(
            "cleanup-test",
            "cleanup-test.v1",
            key,
            new string('a', 64),
            "{\"isSuccess\":true}",
            1,
            createdAt,
            expiresAt);
}
