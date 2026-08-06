using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Common.Messaging;

public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

public interface ITransactionalCommand<TResponse> : ICommand<TResponse>, ITransactionalRequest;

public interface ITransactionalRequest;

public interface IIdempotentCommand<TResponse> : ITransactionalCommand<TResponse>, IIdempotentRequest;

public interface IIdempotentRequest
{
    string IdempotencyOperation { get; }

    string IdempotencyKey { get; }

    string IdempotencyScope { get; }

    object IdempotencyPayload { get; }

    int ResponseSchemaVersion { get; }

    TimeSpan Retention { get; }
}
