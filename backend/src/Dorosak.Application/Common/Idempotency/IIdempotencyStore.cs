namespace Dorosak.Application.Common.Idempotency;

public enum IdempotencyLookupStatus
{
    NotFound = 0,
    Completed = 1,
    Conflict = 2,
    ResponseSchemaMismatch = 3,
}

public sealed record IdempotencyLookup<T>(IdempotencyLookupStatus Status, T? Response);

public interface IIdempotencyStore
{
    Task<IdempotencyLookup<TResponse>> FindAsync<TResponse>(
        string scope,
        string operation,
        string key,
        object requestPayload,
        int responseSchemaVersion,
        CancellationToken cancellationToken);

    Task StoreAsync<TResponse>(
        string scope,
        string operation,
        string key,
        object requestPayload,
        TResponse response,
        int responseSchemaVersion,
        TimeSpan retention,
        CancellationToken cancellationToken);
}
