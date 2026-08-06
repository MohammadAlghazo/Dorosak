namespace Dorosak.Application.Common.Persistence;

public interface IUnitOfWork
{
    Task<TResponse> ExecuteInTransactionAsync<TResponse>(
        Func<CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken);
}
