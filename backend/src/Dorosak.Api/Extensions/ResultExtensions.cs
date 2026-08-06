using System.Diagnostics;
using System.Globalization;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Microsoft.AspNetCore.Mvc;

namespace Dorosak.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return controller.Ok(new ApiResponse<T>(result.Value));
        }

        int status = result.Failure.Type switch
        {
            ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
            ErrorType.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
            ErrorType.RateLimited => StatusCodes.Status429TooManyRequests,
            ErrorType.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest,
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Type = $"https://dorosak.com/problems/{result.Failure.Code.ToLowerInvariant().Replace('.', '-')}",
            Title = result.Failure.Type.ToString(),
            Detail = result.Failure.Description,
            Instance = controller.HttpContext.Request.Path,
            Extensions =
            {
                ["code"] = result.Failure.Code,
                ["traceId"] = Activity.Current?.TraceId.ToString() ?? controller.HttpContext.TraceIdentifier,
                ["correlationId"] = controller.HttpContext.Items[Middleware.CorrelationIdMiddleware.ItemKey],
            },
        };

        if (result.Failure.ValidationErrors is not null)
        {
            problem.Extensions["errors"] = result.Failure.ValidationErrors;
        }
        if (result.Failure.RetryAfter is { } retryAfter)
        {
            int seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            controller.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        return new ObjectResult(problem) { StatusCode = status };
    }
}

public sealed record ApiResponse<T>(T Data);
