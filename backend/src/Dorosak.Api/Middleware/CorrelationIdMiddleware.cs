using System.Net;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace Dorosak.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HashSet<IPAddress> _trustedClientAddresses;

    public CorrelationIdMiddleware(RequestDelegate next, IOptions<CorrelationIdOptions> options)
    {
        _next = next;
        _trustedClientAddresses = options.Value.TrustedClientAddresses
            .Select(IPAddress.Parse)
            .ToHashSet();
    }

    public const string HeaderName = "X-Correlation-ID";

    public const string ItemKey = "Dorosak.CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = IsTrustedClient(context.Connection.RemoteIpAddress)
            ? TryGetValidCorrelationId(context.Request.Headers[HeaderName].FirstOrDefault())
                ?? Guid.CreateVersion7().ToString("N")
            : Guid.CreateVersion7().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private bool IsTrustedClient(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        IPAddress normalizedAddress = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return _trustedClientAddresses.Contains(normalizedAddress);
    }

    private static string? TryGetValidCorrelationId(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 64)
        {
            return null;
        }

        return candidate.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':')
            ? candidate
            : null;
    }
}
