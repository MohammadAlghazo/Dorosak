using Dorosak.Application.Common.Messaging;
using Dorosak.Application.Common.Persistence;
using MediatR;

namespace Dorosak.Application.Common.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        return request is ITransactionalRequest
            ? unitOfWork.ExecuteInTransactionAsync<TResponse>(token => next(), cancellationToken)
            : next();
    }
}

