using Dorosak.Application.Common.Caching;
using Dorosak.Application.Common.Idempotency;
using Dorosak.Application.Common.Identity;
using Dorosak.Application.Common.Persistence;
using Dorosak.Infrastructure.Caching;
using Dorosak.Infrastructure.Catalog;
using Dorosak.Infrastructure.Idempotency;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dorosak.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string databaseConnection = GetRequiredConnectionString(configuration, "Database");
        string redisConnection = GetRequiredConnectionString(configuration, "Redis");

        services.AddDbContext<DorosakDbContext>(options =>
            DatabaseConfiguration.Configure(options, databaseConnection));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DorosakDbContext>());
        services.AddScoped<IIdempotencyStore, EfCoreIdempotencyStore>();
        services.AddScoped<Phase6Service>();
        services.AddScoped<Application.Features.Phase6.IPhase6Service>(provider => provider.GetRequiredService<Phase6Service>());
        services.AddScoped<Application.Features.Phase6.ICourseAccessReader>(provider => provider.GetRequiredService<Phase6Service>());
        services.AddSingleton<CatalogCursorCodec>();
        services.AddSingleton<SearchTelemetry>();
        services.AddOptions<CatalogCursorOptions>()
            .Bind(configuration.GetSection(CatalogCursorOptions.SectionName))
            .Validate(options => options.SigningKey.Length >= 32, "The catalog cursor signing key must contain at least 32 characters.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Environment), "The catalog cursor environment is required.")
            .ValidateOnStart();

        services
            .AddOptions<IdentitySecurityOptions>()
            .Bind(configuration.GetSection(IdentitySecurityOptions.SectionName))
            .Validate(options => options.AccessTokenMinutes is >= 5 and <= 15, "Access token lifetime must be 5-15 minutes.")
            .Validate(options => options.RefreshIdleDays > 0 && options.RefreshAbsoluteDays >= options.RefreshIdleDays,
                "Refresh lifetimes are invalid.")
            .Validate(options => options.RefreshRaceWindowSeconds is >= 1 and <= 30,
                "Refresh race window must be 1-30 seconds.")
            .Validate(options => options.RecentAuthenticationMinutes is >= 5 and <= 30,
                "Recent authentication lifetime must be 5-30 minutes.")
            .ValidateOnStart();
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName)).ValidateOnStart();
        services.AddOptions<ApplicationOptions>().Bind(configuration.GetSection(ApplicationOptions.SectionName)).ValidateOnStart();
        services.AddOptions<SecurityRateLimitOptions>()
            .Bind(configuration.GetSection(SecurityRateLimitOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.KeyPrefix), "Rate-limit key prefix is required.")
            .ValidateOnStart();
        services.AddOptions<PasswordBreachOptions>()
            .Bind(configuration.GetSection(PasswordBreachOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "Password breach URL is invalid.")
            .ValidateOnStart();
        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.SmtpHost), "SMTP host is required.")
            .Validate(options => options.SmtpPort is > 0 and <= 65535, "SMTP port is invalid.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.FromAddress), "Email sender is required.")
            .ValidateOnStart();
        services
            .AddOptions<AdminBootstrapOptions>()
            .Bind(configuration.GetSection(AdminBootstrapOptions.SectionName))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Email),
                "Admin bootstrap email is required when enabled.")
            .Validate(options => !options.Enabled || options.DisplayName.Trim().Length is >= 2 and <= 100,
                "Admin bootstrap display name is invalid.")
            .Validate(options => !options.Enabled || options.TemporaryPassword.Length is >= 14 and <= 64,
                "Admin bootstrap password must contain 14-64 characters.")
            .Validate(options => !options.Enabled || options.TotpSecret.Length >= 16,
                "Admin bootstrap TOTP secret is required when enabled.")
            .ValidateOnStart();

        services
            .AddDataProtection()
            .SetApplicationName("Dorosak")
            .PersistKeysToDbContext<DorosakDbContext>();
        IdentityBuilder identityBuilder = services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.SignIn.RequireConfirmedEmail = true;
                options.Tokens.EmailConfirmationTokenProvider = "DorosakEmailVerification";
                options.Tokens.PasswordResetTokenProvider = "DorosakPasswordReset";
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<DorosakDbContext>()
            .AddDefaultTokenProviders();
        identityBuilder
            .AddTokenProvider<EmailVerificationTokenProvider>("DorosakEmailVerification")
            .AddTokenProvider<PasswordResetTokenProvider>("DorosakPasswordReset");
        services.Configure<PasswordHasherOptions>(options =>
            options.IterationCount = configuration.GetValue("Identity:PasswordHashIterations", 210000));
        services.AddSingleton<IJwtKeyProvider, JwtKeyProvider>();
        services.AddSingleton<JwtTokenIssuer>();
        services.AddSingleton<SecurityRateLimiter>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IIdentitySessionValidator, IdentitySessionValidator>();
        services.AddScoped<IIdentityEmailDispatcher, IdentityEmailDispatcher>();
        services.AddScoped<IAdminBootstrapService, AdminBootstrapService>();

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "dorosak:";
        });
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(
                configuration.GetConnectionString("RedisSecurity") ?? redisConnection));
        services.AddScoped<IQueryCache, DistributedQueryCache>();

        services.ConfigureHttpClientDefaults(httpClient => httpClient.AddStandardResilienceHandler());
        services.AddHttpClient<BreachedPasswordService>((provider, client) =>
        {
            PasswordBreachOptions options = provider.GetRequiredService<IOptions<PasswordBreachOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Dorosak-Security/1.0");
            client.Timeout = TimeSpan.FromSeconds(5);
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
}
