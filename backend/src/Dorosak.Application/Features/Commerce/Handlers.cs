using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.Commerce;

internal sealed class CommerceHandler<TRequest, TResponse>(ICommerceService service)
    : IRequestHandler<TRequest, Result<TResponse>>
    where TRequest : class, IRequest<Result<TResponse>>
    where TResponse : notnull
{
    public Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken) => request switch
    {
        CreateDemoCheckoutCommand command => Cast(service.CreateDemoCheckoutAsync(command, cancellationToken)),
        GetDemoSubscriptionQuery query => Cast(service.GetDemoSubscriptionAsync(query, cancellationToken)),
        ActivateDemoSubscriptionCommand command => Cast(service.ActivateDemoSubscriptionAsync(command, cancellationToken)),
        CancelDemoSubscriptionCommand command => Cast(service.CancelDemoSubscriptionAsync(command, cancellationToken)),
        _ => throw new InvalidOperationException($"Unsupported commerce request {typeof(TRequest).Name}."),
    };

    private static async Task<Result<TResponse>> Cast<TValue>(Task<Result<TValue>> task)
    {
        Result<TValue> result = await task;
        return result.IsSuccess
            ? Result.Success((TResponse)(object)result.Value!)
            : Result.Failure<TResponse>(result.Failure);
    }
}
