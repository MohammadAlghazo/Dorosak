using Dorosak.Application.Features.Phase6;

namespace Dorosak.Infrastructure.Catalog;

internal sealed class SearchTelemetry
{
    private readonly Lock _gate = new();
    private long _requests;

    public void Record(string locale, string query, int resultCount, TimeSpan latency, string sort, CatalogFilterContract filters)
    {
        _ = locale;
        _ = query;
        _ = resultCount;
        _ = latency;
        _ = sort;
        _ = filters;
        lock (_gate)
        {
            _requests++;
        }
    }

    public long Requests => Interlocked.Read(ref _requests);
}
