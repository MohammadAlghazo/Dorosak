using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dorosak.Application.Common.Idempotency;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Persistence;
using Dorosak.Infrastructure.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Idempotency;

internal sealed class EfCoreIdempotencyStore(DorosakDbContext dbContext, TimeProvider timeProvider)
    : IIdempotencyStore
{
    public async Task<IdempotencyLookup<TResponse>> FindAsync<TResponse>(
        string scope,
        string operation,
        string key,
        object requestPayload,
        int responseSchemaVersion,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Idempotency lookups require an active transaction.");
        }

        string lockIdentity = $"{scope}\u001f{operation}\u001f{key}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockIdentity}, 0))",
            cancellationToken);

        IdempotencyRecord? record = await dbContext.IdempotencyRecords.SingleOrDefaultAsync(
            candidate => candidate.Scope == scope && candidate.Operation == operation && candidate.Key == key,
            cancellationToken);

        if (record is null)
        {
            return new IdempotencyLookup<TResponse>(IdempotencyLookupStatus.NotFound, default);
        }
        if (record.ExpiresAt <= timeProvider.GetUtcNow())
        {
            dbContext.IdempotencyRecords.Remove(record);
            return new IdempotencyLookup<TResponse>(IdempotencyLookupStatus.NotFound, default);
        }

        string requestHash = Hash(requestPayload);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(record.RequestHash),
                Encoding.ASCII.GetBytes(requestHash)))
        {
            return new IdempotencyLookup<TResponse>(IdempotencyLookupStatus.Conflict, default);
        }
        if (record.ResponseSchemaVersion != responseSchemaVersion)
        {
            return new IdempotencyLookup<TResponse>(IdempotencyLookupStatus.ResponseSchemaMismatch, default);
        }

        TResponse? response = JsonSerializer.Deserialize<TResponse>(
            record.ResponsePayload,
            DorosakJsonSerializer.Options);
        return new IdempotencyLookup<TResponse>(IdempotencyLookupStatus.Completed, response);
    }

    public Task StoreAsync<TResponse>(
        string scope,
        string operation,
        string key,
        object requestPayload,
        TResponse response,
        int responseSchemaVersion,
        TimeSpan retention,
        CancellationToken cancellationToken)
    {
        DateTimeOffset createdAt = timeProvider.GetUtcNow();
        var record = IdempotencyRecord.CreateCompleted(
            scope,
            operation,
            key,
            Hash(requestPayload),
            JsonSerializer.Serialize(response, DorosakJsonSerializer.Options),
            responseSchemaVersion,
            createdAt,
            createdAt.Add(retention));

        dbContext.IdempotencyRecords.Add(record);
        return Task.CompletedTask;
    }

    private static string Hash(object value)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
            value,
            value.GetType(),
            DorosakJsonSerializer.Options);
        return Convert.ToHexStringLower(SHA256.HashData(payload));
    }
}
