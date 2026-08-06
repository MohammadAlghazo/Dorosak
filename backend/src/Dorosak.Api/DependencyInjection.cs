using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Dorosak.Api.ErrorHandling;
using Dorosak.Api.Health;
using Dorosak.Api.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;

namespace Dorosak.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers();
        services.AddAuthorization();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                int status = context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode;
                string code = GetHttpErrorCode(status);
                context.ProblemDetails.Status = status;
                context.ProblemDetails.Instance = context.HttpContext.Request.Path;
                context.ProblemDetails.Title ??= ReasonPhrases.GetReasonPhrase(status);
                context.ProblemDetails.Type ??= $"https://dorosak.com/problems/{code.ToLowerInvariant().Replace('.', '-')}";
                context.ProblemDetails.Extensions.TryAdd("code", code);
                context.ProblemDetails.Extensions["traceId"] =
                    Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
                context.ProblemDetails.Extensions["correlationId"] =
                    context.HttpContext.Items[CorrelationIdMiddleware.ItemKey];
            };
        });
        services
            .AddOptions<CorrelationIdOptions>()
            .Bind(configuration.GetSection("CorrelationId"))
            .Validate(options => options.HasValidAddresses(), "Correlation ID trusted clients must be IP addresses.")
            .ValidateOnStart();

        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1);
                options.AssumeDefaultVersionWhenUnspecified = false;
                options.ReportApiVersions = true;
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Dorosak API",
                Version = "v1",
                Description = "Dorosak educational platform API",
            });
            options.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);
        });

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/problem+json"]);
        });
        services.AddOutputCache(options =>
        {
            options.AddPolicy(ApiConstants.PublicOutputCachePolicy, policy => policy
                .Expire(TimeSpan.FromSeconds(30))
                .SetVaryByHeader("Origin")
                .SetVaryByQuery("api-version"));
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                TimeSpan retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan value)
                    ? value
                    : TimeSpan.FromMinutes(1);
                int retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                context.HttpContext.Response.Headers.RetryAfter =
                    retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

                IProblemDetailsService problemDetailsService =
                    context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Type = "https://dorosak.com/problems/rate-limit-exceeded",
                        Title = "Too Many Requests",
                        Detail = "The request limit was exceeded. Try again later.",
                        Extensions = { ["code"] = "RATE_LIMIT.EXCEEDED" },
                    },
                });
            };
            options.AddPolicy(ApiConstants.PublicRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetRateLimitPartition(context.Connection.RemoteIpAddress),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));
        });

        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(ApiConstants.CorsPolicy, policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            }
        }));

        string redisConnection = GetRequiredConnectionString(configuration, "Redis");
        services
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"])
            .AddCheck<DatabaseMigrationHealthCheck>("database-schema", tags: ["ready", "startup"])
            .AddRedis(redisConnection, name: "redis", tags: ["dependency"]);

        string[] knownProxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
        string[] knownNetworks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 2;

            if (knownProxies.Length > 0 || knownNetworks.Length > 0)
            {
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();
            }

            foreach (string proxy in knownProxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
            foreach (string network in knownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return services;
    }

    private static string GetRequiredConnectionString(IConfiguration configuration, string name)
    {
        string? value = configuration.GetConnectionString(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Connection string '{name}' is required.")
            : value;
    }

    private static string GetRateLimitPartition(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        IPAddress normalizedAddress = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        if (normalizedAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return normalizedAddress.ToString();
        }

        byte[] network = normalizedAddress.GetAddressBytes();
        Array.Clear(network, 8, 8);
        return $"{new IPAddress(network)}/64";
    }

    private static string GetHttpErrorCode(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "HTTP.BAD_REQUEST",
        StatusCodes.Status401Unauthorized => "HTTP.UNAUTHORIZED",
        StatusCodes.Status403Forbidden => "HTTP.FORBIDDEN",
        StatusCodes.Status404NotFound => "HTTP.NOT_FOUND",
        StatusCodes.Status405MethodNotAllowed => "HTTP.METHOD_NOT_ALLOWED",
        StatusCodes.Status406NotAcceptable => "HTTP.NOT_ACCEPTABLE",
        StatusCodes.Status408RequestTimeout => "HTTP.REQUEST_TIMEOUT",
        StatusCodes.Status409Conflict => "HTTP.CONFLICT",
        StatusCodes.Status412PreconditionFailed => "HTTP.PRECONDITION_FAILED",
        StatusCodes.Status413PayloadTooLarge => "HTTP.CONTENT_TOO_LARGE",
        StatusCodes.Status415UnsupportedMediaType => "HTTP.UNSUPPORTED_MEDIA_TYPE",
        StatusCodes.Status422UnprocessableEntity => "HTTP.UNPROCESSABLE_ENTITY",
        StatusCodes.Status429TooManyRequests => "RATE_LIMIT.EXCEEDED",
        StatusCodes.Status500InternalServerError => "SERVER.UNEXPECTED",
        StatusCodes.Status503ServiceUnavailable => "HTTP.SERVICE_UNAVAILABLE",
        _ => $"HTTP.{status}",
    };
}
