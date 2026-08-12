using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Dorosak.Api.Authorization;
using Dorosak.Api.ErrorHandling;
using Dorosak.Api.Health;
using Dorosak.Api.Middleware;
using Dorosak.Api.OpenApi;
using Dorosak.Api.Realtime;
using Dorosak.Api.Startup;
using Dorosak.Application.Features.Communications;
using Dorosak.Application.Features.Publishing;
using Dorosak.Infrastructure.Communications;
using Dorosak.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using StackExchange.Redis;

namespace Dorosak.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        CommunicationsRealtimeOptions realtimeOptions = configuration
            .GetSection(CommunicationsRealtimeOptions.SectionName)
            .Get<CommunicationsRealtimeOptions>() ?? new CommunicationsRealtimeOptions();
        string? realtimeRedisConnection = configuration.GetConnectionString(
            realtimeOptions.Redis.ConnectionStringName);

        services.AddControllers();
        services
            .AddOptions<CommunicationsRealtimeOptions>()
            .Bind(configuration.GetSection(CommunicationsRealtimeOptions.SectionName))
            .Validate(
                options => options.IdleDelay >= TimeSpan.FromMilliseconds(100) &&
                    options.IdleDelay <= TimeSpan.FromMinutes(1),
                "Communications realtime idle delay must be between 100 milliseconds and one minute.")
            .Validate(
                options => options.FailureDelay >= TimeSpan.FromSeconds(1) &&
                    options.FailureDelay <= TimeSpan.FromMinutes(5),
                "Communications realtime failure delay must be between one second and five minutes.")
            .Validate(
                options => options.SessionValidationInterval >= TimeSpan.FromSeconds(5) &&
                    options.SessionValidationInterval <= TimeSpan.FromSeconds(60),
                "Communications session validation interval must be between five and 60 seconds.")
            .Validate(
                options => !options.DispatcherEnabled || options.Redis.Enabled || options.SingleNodeMode,
                "Communications realtime dispatching requires Redis or explicit single-node mode.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Redis.ConnectionStringName),
                "Communications realtime Redis connection-string name is required.")
            .Validate(
                options => HasValidChannelPrefixRoot(options.Redis.ChannelPrefixRoot),
                "Communications realtime Redis channel-prefix root is invalid.")
            .Validate(
                options => !options.Redis.Enabled || !string.IsNullOrWhiteSpace(
                    configuration.GetConnectionString(options.Redis.ConnectionStringName)),
                "Communications realtime Redis connection string is required when the backplane is enabled.")
            .ValidateOnStart();

        var signalRBuilder = services.AddSignalR();
        if (realtimeOptions.Redis.Enabled && !string.IsNullOrWhiteSpace(realtimeRedisConnection))
        {
            string channelPrefix = string.Create(
                CultureInfo.InvariantCulture,
                $"{realtimeOptions.Redis.ChannelPrefixRoot}:{hostEnvironment.EnvironmentName.Trim().ToLowerInvariant()}:v1:communications");
            signalRBuilder.AddStackExchangeRedis(realtimeRedisConnection, options =>
                options.Configuration.ChannelPrefix = RedisChannel.Literal(channelPrefix));
        }

        services.AddSingleton<IUserIdProvider, SubjectUserIdProvider>();
        services.AddSingleton<CommunicationsConnectionRegistry>();
        services.AddSingleton<CommunicationsSessionValidationWorker>();
        services.AddHostedService(provider =>
            provider.GetRequiredService<CommunicationsSessionValidationWorker>());
        services.AddSingleton<ICommunicationsRealtimePublisher, SignalRCommunicationsRealtimePublisher>();
        services.AddCommunicationsRealtimeDispatching();
        if (realtimeOptions.DispatcherEnabled)
        {
            services.AddHostedService<CommunicationsRealtimeWorker>();
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IJwtKeyProvider, IOptions<JwtOptions>>((options, keyProvider, jwtOptions) =>
            {
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Value.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = keyProvider.ValidationKey,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromSeconds(60),
                    ValidTypes = ["at+jwt"],
                    NameClaimType = "sub",
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        string accessToken = context.Request.Query["access_token"].ToString();
                        if (!string.IsNullOrWhiteSpace(accessToken) &&
                            HttpMethods.IsGet(context.Request.Method) &&
                            string.Equals(
                                context.Request.Path.Value,
                                CommunicationsHub.Path,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        IIdentitySessionValidator validator = context.HttpContext.RequestServices
                            .GetRequiredService<IIdentitySessionValidator>();
                        if (!await validator.IsValidAsync(context.Principal!, context.HttpContext.RequestAborted))
                        {
                            context.Fail("The server-side session is invalid.");
                        }
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        IProblemDetailsService problemDetailsService = context.HttpContext.RequestServices
                            .GetRequiredService<IProblemDetailsService>();
                        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
                        {
                            HttpContext = context.HttpContext,
                            ProblemDetails = new ProblemDetails
                            {
                                Status = StatusCodes.Status401Unauthorized,
                                Title = "Unauthorized",
                                Type = "https://dorosak.com/problems/authentication-required",
                                Detail = "Authentication is required.",
                                Extensions = { ["code"] = "AUTH.AUTHENTICATION_REQUIRED" },
                            },
                        });
                    },
                };
            });
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, AdminHighRiskAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, RecentAuthenticationAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationMiddlewareResultHandler>();
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "__Host-dorosak-antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.Path = "/";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.HeaderName = "X-XSRF-TOKEN";
            options.SuppressXFrameOptionsHeader = true;
        });
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
            options.OperationFilter<RequiredIdempotencyHeaderOperationFilter>();
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
            options.AddPolicy(ApiConstants.CatalogOutputCachePolicy, policy => policy
                .Expire(TimeSpan.FromSeconds(60))
                .SetVaryByHeader("Accept-Language")
                .SetVaryByQuery("*")
                .VaryByValue(async (context, cancellationToken) =>
                {
                    ICatalogProjectionGenerationPort generation = context.RequestServices
                        .GetRequiredService<ICatalogProjectionGenerationPort>();
                    long value = await generation.GetAsync(cancellationToken);
                    return new KeyValuePair<string, string>(
                        "catalog-generation",
                        value.ToString(CultureInfo.InvariantCulture));
                })
                .Tag(ApiConstants.CatalogCacheTag));
            options.AddPolicy(ApiConstants.TaxonomyOutputCachePolicy, policy => policy
                .Expire(TimeSpan.FromMinutes(5))
                .SetVaryByHeader("Accept-Language")
                .SetVaryByQuery("*")
                .Tag(ApiConstants.TaxonomyCacheTag));
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
            options.AddPolicy(ApiConstants.SensitiveRateLimitPolicy, context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    context.User.FindFirst("sub")?.Value is { Length: > 0 } userId
                        ? $"user:{userId}"
                        : $"anonymous:{GetRateLimitPartition(context.Connection.RemoteIpAddress)}",
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));
            options.AddPolicy(ApiConstants.SearchRateLimitPolicy, context =>
            {
                string? userId = context.User.FindFirst("sub")?.Value;
                string partition = userId is null
                    ? $"anonymous:{GetRateLimitPartition(context.Connection.RemoteIpAddress)}"
                    : $"user:{userId}";
                int limit = userId is null ? 60 : 180;
                return RateLimitPartition.GetFixedWindowLimiter(
                    partition,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    });
            });
            options.AddPolicy(ApiConstants.UploadRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.FindFirst("sub")?.Value ?? GetRateLimitPartition(context.Connection.RemoteIpAddress),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));
        });

        services.AddTransient<OriginValidationMiddleware>();
        services.AddHostedService<IdentitySecurityStartupCheck>();

        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(ApiConstants.CorsPolicy, policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders("ETag")
                    .AllowCredentials();
            }
        }));

        string redisConnection = GetRequiredConnectionString(configuration, "Redis");
        var healthChecks = services
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"])
            .AddCheck<DatabaseMigrationHealthCheck>("database-schema", tags: ["ready", "startup"])
            .AddRedis(redisConnection, name: "redis", tags: ["dependency"]);
        if (realtimeOptions.Redis.Enabled && !string.IsNullOrWhiteSpace(realtimeRedisConnection))
        {
            healthChecks.AddRedis(
                realtimeRedisConnection,
                name: "redis-realtime",
                tags: ["dependency"]);
        }

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

    private static bool HasValidChannelPrefixRoot(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 64 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

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
        StatusCodes.Status428PreconditionRequired => "HTTP.PRECONDITION_REQUIRED",
        StatusCodes.Status413PayloadTooLarge => "HTTP.CONTENT_TOO_LARGE",
        StatusCodes.Status415UnsupportedMediaType => "HTTP.UNSUPPORTED_MEDIA_TYPE",
        StatusCodes.Status422UnprocessableEntity => "HTTP.UNPROCESSABLE_ENTITY",
        StatusCodes.Status429TooManyRequests => "RATE_LIMIT.EXCEEDED",
        StatusCodes.Status500InternalServerError => "SERVER.UNEXPECTED",
        StatusCodes.Status503ServiceUnavailable => "HTTP.SERVICE_UNAVAILABLE",
        _ => $"HTTP.{status}",
    };
}
