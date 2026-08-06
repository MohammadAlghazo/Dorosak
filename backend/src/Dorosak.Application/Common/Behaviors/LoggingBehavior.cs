using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Dorosak.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly Action<ILogger, string, double, Exception?> RequestCompleted =
        LoggerMessage.Define<string, double>(
            LogLevel.Information,
            new EventId(1000, nameof(RequestCompleted)),
            "Application request {RequestName} completed in {ElapsedMilliseconds} ms");

    private static readonly Action<ILogger, string, double, Exception?> RequestCancelled =
        LoggerMessage.Define<string, double>(
            LogLevel.Debug,
            new EventId(1001, nameof(RequestCancelled)),
            "Application request {RequestName} was cancelled after {ElapsedMilliseconds} ms");

    private static readonly Action<ILogger, string, double, Exception?> RequestFailed =
        LoggerMessage.Define<string, double>(
            LogLevel.Warning,
            new EventId(1002, nameof(RequestFailed)),
            "Application request {RequestName} failed after {ElapsedMilliseconds} ms");

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        long startedAt = Stopwatch.GetTimestamp();
        string requestName = typeof(TRequest).Name;

        try
        {
            TResponse response = await next(cancellationToken);
            RequestCompleted(
                logger,
                requestName,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                null);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            RequestCancelled(
                logger,
                requestName,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                null);
            throw;
        }
        catch (Exception)
        {
            RequestFailed(
                logger,
                requestName,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                null);
            throw;
        }
    }
}
