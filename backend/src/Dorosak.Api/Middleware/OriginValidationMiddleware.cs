using Dorosak.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dorosak.Api.Middleware;

public sealed class OriginValidationMiddleware(
    IOptions<ApplicationOptions> applicationOptions,
    IProblemDetailsService problemDetailsService) : IMiddleware
{
    private static readonly HashSet<string> SafeMethods =
        new(["GET", "HEAD", "OPTIONS"], StringComparer.OrdinalIgnoreCase);

    private readonly string _trustedOrigin = new Uri(applicationOptions.Value.PublicUrl, UriKind.Absolute)
        .GetLeftPart(UriPartial.Authority);

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        bool isAuthMutation = context.Request.Path.StartsWithSegments("/api/v1/auth");
        bool isSessionMutation = context.Request.Path.StartsWithSegments("/api/v1/me/sessions");
        if ((!isAuthMutation && !isSessionMutation) || SafeMethods.Contains(context.Request.Method))
        {
            await next(context);
            return;
        }

        string? origin = context.Request.Headers.Origin.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(origin) ||
            !string.Equals(origin.TrimEnd('/'), _trustedOrigin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Type = "https://dorosak.com/problems/origin-rejected",
                    Detail = "The request origin is not trusted.",
                    Extensions = { ["code"] = "SECURITY.ORIGIN_REJECTED" },
                },
            });
            return;
        }

        await next(context);
    }
}
