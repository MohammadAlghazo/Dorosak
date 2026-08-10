using Dorosak.Application.Common.Authorization;
using Dorosak.Application.Common.Exceptions;
using MediatR;

namespace Dorosak.Application.Common.Behaviors;

public sealed class AuthorizationBehavior<TRequest, TResponse>(IEnumerable<IRequestAuthorizer<TRequest>> authorizers)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IAuthorizedRequest)
        {
            return await next(cancellationToken);
        }

        IRequestAuthorizer<TRequest>[] requestAuthorizers = [.. authorizers];
        if (requestAuthorizers.Length == 0)
        {
            throw new InvalidOperationException($"Authorized request {typeof(TRequest).Name} has no authorizer.");
        }

        foreach (IRequestAuthorizer<TRequest> authorizer in requestAuthorizers)
        {
            AuthorizationDecision decision = await authorizer.AuthorizeAsync(request, cancellationToken);
            if (!decision.IsAllowed)
            {
                if (decision.Code.EndsWith(".ACCESS_DENIED", StringComparison.Ordinal))
                {
                    string resource = decision.Code[..^".ACCESS_DENIED".Length];
                    throw new ResourceNotFoundException($"{resource}.NOT_FOUND", decision.Description);
                }
                throw new ForbiddenAccessException(decision.Code, decision.Description);
            }
        }

        return await next(cancellationToken);
    }
}
