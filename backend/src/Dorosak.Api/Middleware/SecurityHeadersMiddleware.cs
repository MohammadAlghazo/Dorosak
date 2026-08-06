namespace Dorosak.Api.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        IHeaderDictionary headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers.ContentSecurityPolicy = context.Request.Path.StartsWithSegments("/swagger")
            ? "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; " +
              "img-src 'self' data:; font-src 'self'; connect-src 'self'; object-src 'none'; " +
              "frame-ancestors 'none'; base-uri 'none'"
            : "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
        headers.Append("Referrer-Policy", "no-referrer");
        headers.Append("Permissions-Policy", "camera=(), geolocation=(), microphone=()");
        headers.Append("Cross-Origin-Opener-Policy", "same-origin");
        headers.Append("Cross-Origin-Resource-Policy", "same-site");

        await next(context);
    }
}
