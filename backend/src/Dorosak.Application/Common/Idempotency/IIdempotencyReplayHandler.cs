namespace Dorosak.Application.Common.Idempotency;

public interface IIdempotencyReplayHandler<in TRequest, TResponse>
{
    Task<TResponse> ResolveAsync(
        TRequest request,
        TResponse storedResponse,
        CancellationToken cancellationToken);
}
