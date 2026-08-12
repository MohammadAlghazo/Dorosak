using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.Credentials;

internal sealed class CredentialsHandler<TRequest, TResponse>(ICredentialsService service)
    : IRequestHandler<TRequest, Result<TResponse>>
    where TRequest : class, IRequest<Result<TResponse>>
    where TResponse : notnull
{
    public Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken) => request switch
    {
        GetMyCertificatesQuery query => Cast(service.GetMyCertificatesAsync(query, cancellationToken)),
        GetMyCertificateQuery query => Cast(service.GetMyCertificateAsync(query, cancellationToken)),
        VerifyCertificateQuery query => Cast(service.VerifyCertificateAsync(query, cancellationToken)),
        IssueCertificateFromCompletionCommand command => Cast(
            service.IssueCertificateFromCompletionAsync(command, cancellationToken)),
        RevokeCertificateCommand command => Cast(service.RevokeCertificateAsync(command, cancellationToken)),
        _ => throw new InvalidOperationException($"Unsupported credentials request {typeof(TRequest).Name}."),
    };

    private static async Task<Result<TResponse>> Cast<TValue>(Task<Result<TValue>> task)
    {
        Result<TValue> result = await task;
        return result.IsSuccess
            ? Result.Success((TResponse)(object)result.Value!)
            : Result.Failure<TResponse>(result.Failure);
    }
}
