using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.Moderation;

internal sealed class ModerationHandler<TRequest, TResponse>(IModerationService service)
    : IRequestHandler<TRequest, Result<TResponse>>
    where TRequest : IRequest<Result<TResponse>>
    where TResponse : notnull
{
    public Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken) => request switch
    {
        CreateContentReportCommand command => Cast(service.CreateContentReportAsync(command, cancellationToken)),
        GetMyContentReportQuery query => Cast(service.GetMyContentReportAsync(query, cancellationToken)),
        GetAdminContentReportsQuery query => Cast(service.GetAdminContentReportsAsync(query, cancellationToken)),
        GetModerationCasesQuery query => Cast(service.GetModerationCasesAsync(query, cancellationToken)),
        GetModerationCaseQuery query => Cast(service.GetModerationCaseAsync(query, cancellationToken)),
        ApplyModerationActionCommand command => Cast(service.ApplyModerationActionAsync(command, cancellationToken)),
        _ => throw new InvalidOperationException($"Unsupported moderation request {typeof(TRequest).Name}."),
    };

    private static async Task<Result<TResponse>> Cast<TActual>(Task<Result<TActual>> task)
    {
        Result<TActual> result = await task;
        return (Result<TResponse>)(object)result;
    }
}
