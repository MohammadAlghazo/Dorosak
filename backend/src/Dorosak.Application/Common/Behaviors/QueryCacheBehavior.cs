using Dorosak.Application.Common.Caching;
using Dorosak.Application.Common.Messaging;
using Dorosak.Application.Common.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Dorosak.Application.Common.Behaviors;

public sealed class QueryCacheBehavior<TRequest, TResponse>(
    IQueryCache cache,
    ILogger<QueryCacheBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly Action<ILogger, string, Exception?> CacheReadFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1200, nameof(CacheReadFailed)),
        "Cache read failed for query {RequestName}; continuing without cache");

    private static readonly Action<ILogger, string, Exception?> CacheWriteFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1201, nameof(CacheWriteFailed)),
        "Cache write failed for query {RequestName}; returning the uncached response");

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICachedQuery cachedQuery)
        {
            return await next(cancellationToken);
        }

        CacheLookup<TResponse> lookup;
        try
        {
            lookup = await cache.GetAsync<TResponse>(cachedQuery.CacheKey, cancellationToken);
        }
        catch (Exception exception) when (!IsRequestCancellation(exception, cancellationToken))
        {
            CacheReadFailed(logger, typeof(TRequest).Name, exception);
            lookup = new CacheLookup<TResponse>(false, default);
        }

        if (lookup.Found)
        {
            return lookup.Value!;
        }

        TResponse response = await next(cancellationToken);
        if (response is not IResult result || result.IsSuccess)
        {
            try
            {
                await cache.SetAsync(cachedQuery.CacheKey, response, cachedQuery.CacheDuration, cancellationToken);
            }
            catch (Exception exception) when (!IsRequestCancellation(exception, cancellationToken))
            {
                CacheWriteFailed(logger, typeof(TRequest).Name, exception);
            }
        }

        return response;
    }

    private static bool IsRequestCancellation(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
}
