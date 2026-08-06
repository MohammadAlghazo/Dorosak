using Dorosak.Application.Common.Behaviors;
using Dorosak.Application.Common.Caching;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Messaging;
using Dorosak.Application.Common.Results;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dorosak.Application.UnitTests.Common.Behaviors;

public sealed class QueryCacheBehaviorTests
{
    [Fact]
    public async Task Handle_ReturnsCachedResponseWithoutCallingHandler()
    {
        Result<string> cachedResponse = Result.Success("cached");
        var cache = new StubQueryCache { Found = true, CachedValue = cachedResponse };
        var behavior = CreateBehavior(cache);
        int handlerCalls = 0;

        Result<string> result = await behavior.Handle(
            new CachedQuery(),
            _ =>
            {
                handlerCalls++;
                return Task.FromResult(Result.Success("database"));
            },
            TestContext.Current.CancellationToken);

        Assert.Same(cachedResponse, result);
        Assert.Equal(0, handlerCalls);
        Assert.Equal(0, cache.SetCalls);
    }

    [Fact]
    public async Task Handle_FallsBackWhenCacheReadAndWriteFail()
    {
        var cache = new StubQueryCache
        {
            GetException = new InvalidOperationException("read unavailable"),
            SetException = new InvalidOperationException("write unavailable"),
        };
        var behavior = CreateBehavior(cache);

        Result<string> result = await behavior.Handle(
            new CachedQuery(),
            _ => Task.FromResult(Result.Success("database")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("database", result.Value);
        Assert.Equal(1, cache.SetCalls);
    }

    [Fact]
    public async Task Handle_DoesNotCacheFailureResults()
    {
        var cache = new StubQueryCache();
        var behavior = CreateBehavior(cache);

        Result<string> result = await behavior.Handle(
            new CachedQuery(),
            _ => Task.FromResult(Result.Failure<string>(ResultError.NotFound("ITEM.NOT_FOUND", "Not found."))),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, cache.SetCalls);
    }

    [Fact]
    public async Task Handle_PropagatesRequestCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cache = new StubQueryCache
        {
            GetException = new OperationCanceledException(cancellation.Token),
        };
        var behavior = CreateBehavior(cache);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => behavior.Handle(
            new CachedQuery(),
            _ => Task.FromResult(Result.Success("database")),
            cancellation.Token));
    }

    private static QueryCacheBehavior<CachedQuery, Result<string>> CreateBehavior(StubQueryCache cache) =>
        new(cache, NullLogger<QueryCacheBehavior<CachedQuery, Result<string>>>.Instance);

    private sealed record CachedQuery : ICachedQuery
    {
        public string CacheKey => "test:query";

        public TimeSpan CacheDuration => TimeSpan.FromMinutes(1);
    }

    private sealed class StubQueryCache : IQueryCache
    {
        public bool Found { get; init; }

        public object? CachedValue { get; init; }

        public Exception? GetException { get; init; }

        public Exception? SetException { get; init; }

        public int SetCalls { get; private set; }

        public ValueTask<CacheLookup<T>> GetAsync<T>(string key, CancellationToken cancellationToken)
        {
            return GetException is not null
                ? ValueTask.FromException<CacheLookup<T>>(GetException)
                : ValueTask.FromResult(new CacheLookup<T>(Found, (T?)CachedValue));
        }

        public ValueTask SetAsync<T>(
            string key,
            T value,
            TimeSpan duration,
            CancellationToken cancellationToken)
        {
            SetCalls++;
            return SetException is not null
                ? ValueTask.FromException(SetException)
                : ValueTask.CompletedTask;
        }

        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
