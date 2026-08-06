using System.Text.Json;
using Dorosak.Application.Common.Caching;
using Dorosak.Infrastructure.Serialization;
using Microsoft.Extensions.Caching.Distributed;

namespace Dorosak.Infrastructure.Caching;

internal sealed class DistributedQueryCache(IDistributedCache cache) : IQueryCache
{
    public async ValueTask<CacheLookup<T>> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        byte[]? payload = await cache.GetAsync(key, cancellationToken);
        if (payload is null)
        {
            return new CacheLookup<T>(false, default);
        }

        T? value = JsonSerializer.Deserialize<T>(payload, DorosakJsonSerializer.Options);
        return new CacheLookup<T>(true, value);
    }

    public async ValueTask SetAsync<T>(
        string key,
        T value,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(value, DorosakJsonSerializer.Options);
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = duration };
        await cache.SetAsync(key, payload, options, cancellationToken);
    }

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken) =>
        new(cache.RemoveAsync(key, cancellationToken));
}
