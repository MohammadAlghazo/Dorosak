using System.Diagnostics;
using Dorosak.Application.Common.Telemetry;
using MediatR;

namespace Dorosak.Application.Common.Behaviors;

public sealed class TelemetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        using Activity? activity = ApplicationTelemetry.ActivitySource.StartActivity(typeof(TRequest).Name);
        activity?.SetTag("application.request.type", typeof(TRequest).FullName);

        try
        {
            TResponse response = await next(cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            throw;
        }
    }
}
