using Dorosak.Application.Common.Exceptions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Dorosak.Api.ErrorHandling;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, Exception?> UnexpectedException = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(5000, nameof(UnexpectedException)),
        "An unhandled exception reached the API boundary");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        ProblemDetails problemDetails = exception switch
        {
            ApplicationValidationException validation => CreateValidationProblem(validation),
            AntiforgeryValidationException => CreateProblem(
                StatusCodes.Status400BadRequest,
                "SECURITY.ANTIFORGERY_INVALID",
                "Bad Request",
                "The antiforgery token is missing or invalid."),
            ForbiddenAccessException forbidden => CreateProblem(
                StatusCodes.Status403Forbidden,
                forbidden.Code,
                "Forbidden",
                forbidden.Message),
            RequestConflictException conflict => CreateProblem(
                StatusCodes.Status409Conflict,
                conflict.Code,
                "Conflict",
                conflict.Message),
            OperationCanceledException => CreateProblem(
                StatusCodes.Status503ServiceUnavailable,
                "REQUEST.CANCELLED",
                "Service Unavailable",
                "The request could not be completed."),
            _ => CreateUnexpectedProblem(exception),
        };

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        });
    }

    private ProblemDetails CreateUnexpectedProblem(Exception exception)
    {
        UnexpectedException(logger, exception);
        return CreateProblem(
            StatusCodes.Status500InternalServerError,
            "SERVER.UNEXPECTED",
            "Internal Server Error",
            "An unexpected error occurred.");
    }

    private static ValidationProblemDetails CreateValidationProblem(ApplicationValidationException exception)
    {
        return new ValidationProblemDetails(exception.Errors.ToDictionary())
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Type = "https://dorosak.com/problems/validation-failed",
            Title = "Validation Failed",
            Detail = "One or more validation errors occurred.",
            Extensions = { ["code"] = "VALIDATION.FAILED" },
        };
    }

    private static ProblemDetails CreateProblem(int status, string code, string title, string detail)
    {
        return new ProblemDetails
        {
            Status = status,
            Type = $"https://dorosak.com/problems/{code.ToLowerInvariant().Replace('.', '-')}",
            Title = title,
            Detail = detail,
            Extensions = { ["code"] = code },
        };
    }
}
