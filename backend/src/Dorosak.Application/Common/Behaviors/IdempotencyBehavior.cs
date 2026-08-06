using Dorosak.Application.Common.Exceptions;
using Dorosak.Application.Common.Idempotency;
using Dorosak.Application.Common.Messaging;
using MediatR;

namespace Dorosak.Application.Common.Behaviors;

public sealed class IdempotencyBehavior<TRequest, TResponse>(IIdempotencyStore store)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IIdempotentRequest idempotentRequest)
        {
            return await next(cancellationToken);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(idempotentRequest.IdempotencyOperation);
        ArgumentNullException.ThrowIfNull(idempotentRequest.IdempotencyPayload);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(idempotentRequest.ResponseSchemaVersion);

        IdempotencyLookup<TResponse> lookup = await store.FindAsync<TResponse>(
            idempotentRequest.IdempotencyScope,
            idempotentRequest.IdempotencyOperation,
            idempotentRequest.IdempotencyKey,
            idempotentRequest.IdempotencyPayload,
            idempotentRequest.ResponseSchemaVersion,
            cancellationToken);

        if (lookup.Status == IdempotencyLookupStatus.Conflict)
        {
            throw new RequestConflictException(
                "IDEMPOTENCY.KEY_REUSED",
                "The idempotency key was already used with a different request.");
        }
        if (lookup.Status == IdempotencyLookupStatus.Completed)
        {
            return lookup.Response!;
        }
        if (lookup.Status == IdempotencyLookupStatus.ResponseSchemaMismatch)
        {
            throw new RequestConflictException(
                "IDEMPOTENCY.RESPONSE_SCHEMA_MISMATCH",
                "The stored response is not compatible with this operation version.");
        }

        TResponse response = await next(cancellationToken);
        await store.StoreAsync(
            idempotentRequest.IdempotencyScope,
            idempotentRequest.IdempotencyOperation,
            idempotentRequest.IdempotencyKey,
            idempotentRequest.IdempotencyPayload,
            response,
            idempotentRequest.ResponseSchemaVersion,
            idempotentRequest.Retention,
            cancellationToken);

        return response;
    }
}
