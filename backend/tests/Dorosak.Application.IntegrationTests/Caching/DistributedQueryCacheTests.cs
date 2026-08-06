using Dorosak.Application.Common.Caching;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Dorosak.Application.IntegrationTests.Caching;

[Collection(InfrastructureTestGroup.Name)]
public sealed class DistributedQueryCacheTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Cache_RoundTripsSuccessfulAndFailedResults()
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        IQueryCache cache = scope.ServiceProvider.GetRequiredService<IQueryCache>();
        string successKey = $"test:success:{Guid.CreateVersion7():N}";
        string failureKey = $"test:failure:{Guid.CreateVersion7():N}";
        var payload = new CachePayload("course-1", "Distributed Systems");
        Result<CachePayload> successful = Result.Success(payload);
        Result<CachePayload> failed = Result.Failure<CachePayload>(
            ResultError.NotFound("COURSE.NOT_FOUND", "The course was not found."));

        await cache.SetAsync(successKey, successful, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        await cache.SetAsync(failureKey, failed, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

        CacheLookup<Result<CachePayload>> successfulLookup = await cache.GetAsync<Result<CachePayload>>(
            successKey,
            TestContext.Current.CancellationToken);
        CacheLookup<Result<CachePayload>> failedLookup = await cache.GetAsync<Result<CachePayload>>(
            failureKey,
            TestContext.Current.CancellationToken);

        Assert.True(successfulLookup.Found);
        Assert.NotNull(successfulLookup.Value);
        Assert.True(successfulLookup.Value.IsSuccess);
        Assert.Equal(payload, successfulLookup.Value.Value);
        Assert.True(failedLookup.Found);
        Assert.NotNull(failedLookup.Value);
        Assert.False(failedLookup.Value.IsSuccess);
        Assert.Equal("COURSE.NOT_FOUND", failedLookup.Value.Failure.Code);

        await cache.RemoveAsync(successKey, TestContext.Current.CancellationToken);
        await cache.RemoveAsync(failureKey, TestContext.Current.CancellationToken);
    }

    private sealed record CachePayload(string Id, string Title);
}
