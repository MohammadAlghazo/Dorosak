using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Common.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;

public interface ICachedQuery
{
    string CacheKey { get; }

    TimeSpan CacheDuration { get; }
}
