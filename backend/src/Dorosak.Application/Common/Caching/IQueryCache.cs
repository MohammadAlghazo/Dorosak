namespace Dorosak.Application.Common.Caching;

public readonly record struct CacheLookup<T>(bool Found, T? Value);

public interface IQueryCache
{
    ValueTask<CacheLookup<T>> GetAsync<T>(string key, CancellationToken cancellationToken);

    ValueTask SetAsync<T>(string key, T value, TimeSpan duration, CancellationToken cancellationToken);

    ValueTask RemoveAsync(string key, CancellationToken cancellationToken);
}
