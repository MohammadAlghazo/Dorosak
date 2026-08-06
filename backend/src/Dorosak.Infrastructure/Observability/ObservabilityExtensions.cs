using Dorosak.Application.Common.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Dorosak.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddDorosakObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        bool includeAspNetCoreInstrumentation)
    {
        Uri? otlpEndpoint = GetOtlpEndpoint(configuration);
        var openTelemetry = services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName));

        openTelemetry.WithTracing(tracing =>
        {
            tracing
                .AddSource(ApplicationTelemetry.ActivitySourceName)
                .AddHttpClientInstrumentation()
                .AddNpgsql();

            if (includeAspNetCoreInstrumentation)
            {
                tracing.AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health");
                });
            }

            if (otlpEndpoint is not null)
            {
                tracing.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
            }
        });

        openTelemetry.WithMetrics(metrics =>
        {
            metrics
                .AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsqlInstrumentation();

            if (includeAspNetCoreInstrumentation)
            {
                metrics.AddAspNetCoreInstrumentation();
            }

            if (otlpEndpoint is not null)
            {
                metrics.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
            }
        });

        return services;
    }

    private static Uri? GetOtlpEndpoint(IConfiguration configuration)
    {
        string? endpoint = configuration["OpenTelemetry:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("OpenTelemetry:Endpoint must be an absolute HTTP or HTTPS URI.");
        }

        return uri;
    }
}
