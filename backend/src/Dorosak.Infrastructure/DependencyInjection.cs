using Dorosak.Application.Common.Caching;
using Dorosak.Application.Common.Idempotency;
using Dorosak.Application.Common.Identity;
using Dorosak.Application.Common.Persistence;
using Dorosak.Application.Features.Media;
using Dorosak.Application.Features.Publishing;
using Dorosak.Infrastructure.Administration;
using Dorosak.Infrastructure.Analytics;
using Dorosak.Infrastructure.Caching;
using Dorosak.Infrastructure.Catalog;
using Dorosak.Infrastructure.Commerce;
using Dorosak.Infrastructure.Communications;
using Dorosak.Infrastructure.Credentials;
using Dorosak.Infrastructure.Engagement;
using Dorosak.Infrastructure.Idempotency;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Learning;
using Dorosak.Infrastructure.Media;
using Dorosak.Infrastructure.Moderation;
using Dorosak.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
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
        services.AddScoped<IIdempotencyRecordCleaner, EfCoreIdempotencyRecordCleaner>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IMediaAccessReader, MediaAccessReader>();
        services.AddScoped<IMediaJobStore, MediaJobStore>();
        services.AddScoped<IMediaProcessingStore, MediaProcessingStore>();
        services.AddScoped<Application.Features.Learning.ILearningService, LearningService>();
        services.AddScoped<Application.Features.Commerce.ICommerceService, CommerceService>();
        services.AddScoped<Application.Features.Credentials.ICredentialsService, CredentialsService>();
        services.AddScoped<Application.Features.Credentials.ICertificateIssuanceDispatcher, CertificateIssuanceDispatcher>();
        services.AddScoped<CommunicationsService>();
        services.AddScoped<Application.Features.Communications.ICommunicationsService>(provider =>
            provider.GetRequiredService<CommunicationsService>());
        services.AddScoped<Application.Features.Communications.IConversationAccessReader>(provider =>
            provider.GetRequiredService<CommunicationsService>());
        services.AddScoped<Application.Features.Communications.IAnnouncementAccessReader>(provider =>
            provider.GetRequiredService<CommunicationsService>());
        services.AddScoped<EngagementService>();
        services.AddScoped<Application.Features.Engagement.IEngagementService>(provider =>
            provider.GetRequiredService<EngagementService>());
        services.AddScoped<Application.Features.Engagement.IDiscussionAccessReader>(provider =>
            provider.GetRequiredService<EngagementService>());
        services.AddScoped<Application.Features.Moderation.IModerationService, ModerationService>();
        services.AddScoped<Application.Features.Analytics.IAnalyticsService, AnalyticsService>();
        services.AddScoped<Application.Features.Administration.IAdministrationService, AdministrationService>();
        services.AddScoped<Phase6Service>();
        services.AddScoped<Application.Features.Phase6.IPhase6Service>(provider => provider.GetRequiredService<Phase6Service>());
        services.AddScoped<Application.Features.Phase6.ICourseAccessReader>(provider => provider.GetRequiredService<Phase6Service>());
        services.AddScoped<IAuthoringPublishingPort, AuthoringPublishingPort>();
        services.AddScoped<IMediaPublishingPort, MediaPublishingPort>();
        services.AddScoped<IAssessmentPublishingPort, AssessmentPublishingPort>();
        services.AddScoped<IPublishingAuditPort, PublishingAuditPort>();
        services.AddScoped<ICatalogProjectionGenerationPort, CatalogProjectionGenerationPort>();
        services.AddScoped<ICatalogActivationPort, CatalogActivationPort>();
        services.AddScoped<IPublicCatalogPort>(provider => provider.GetRequiredService<Phase6Service>());
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
        services.AddOptions<MediaOptions>()
            .Bind(configuration.GetSection(MediaOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Environment), "Media environment is required.")
            .Validate(options => options.MaxStreamBytes > 0 && options.MaxStreamBytes <= 32L * 1024 * 1024,
                "Media stream limit must be between 1 byte and 32 MiB.")
            .Validate(options => options.MultipartMinimumBytes >= 8L * 1024 * 1024,
                "Multipart uploads must be at least 8 MiB.")
            .Validate(options => options.PartSizeBytes >= options.MultipartMinimumBytes && options.PartSizeBytes <= 64L * 1024 * 1024,
                "Media part size must be between the multipart minimum and 64 MiB.")
            .Validate(options => options.MaxPartBytes >= options.PartSizeBytes && options.MaxPartBytes <= 64L * 1024 * 1024,
                "Media maximum part size is invalid.")
            .Validate(options => options.ProfileImageMaxBytes > 0 && options.CourseImageMaxBytes > 0 &&
                options.CaptionMaxBytes > 0 && options.CourseDocumentMaxBytes > 0 &&
                options.AssignmentSubmissionMaxBytes > 0 && options.SourceVideoMaxBytes > 0,
                "Media purpose limits must be positive and bounded.")
             .Validate(options => options.ProfileImageMaxBytes <= 10L * 1024 * 1024 &&
                options.CourseImageMaxBytes <= 20L * 1024 * 1024 && options.CaptionMaxBytes <= 10L * 1024 * 1024 &&
                options.CourseDocumentMaxBytes <= 100L * 1024 * 1024 &&
                options.AssignmentSubmissionMaxBytes <= 250L * 1024 * 1024 && options.SourceVideoMaxBytes <= 10L * 1024 * 1024 * 1024,
                 "Media purpose limits exceed the phase contract.")
            .Validate(options => options.AssignmentSubmissionMaxFiles is >= 1 and <= 20,
                "Assignment submission file count limit is invalid.")
            .Validate(options => options.SessionTtl > TimeSpan.Zero && options.SessionTtl <= TimeSpan.FromDays(7),
                "Media session TTL must be positive and no longer than seven days.")
            .Validate(options => options.TeacherQuotaBytes > 0 && options.CourseQuotaBytes > 0 && options.StudentQuotaBytes > 0,
                "Media quotas must be positive.")
            .Validate(options => options.TeacherDailyQuotaBytes > 0 && options.StudentDailyQuotaBytes > 0,
                "Media daily quotas must be positive.")
            .Validate(options => options.MaxConcurrentSessions > 0 && options.MaxConcurrentSessions <= 20,
                "Media concurrent session limit is invalid.")
            .Validate(options => options.WorkerMaxAttempts is >= 1 and <= 10 && options.WorkerConcurrency is >= 1 and <= 16,
                "Media worker limits are invalid.")
            .Validate(options => options.WorkerLockDuration > TimeSpan.Zero && options.WorkerLockDuration <= TimeSpan.FromMinutes(15),
                "Media worker lock duration is invalid.")
            .Validate(options => options.OrphanGracePeriod >= TimeSpan.FromHours(1) && options.OrphanGracePeriod <= TimeSpan.FromDays(7),
                "Media orphan grace period is invalid.")
            .Validate(options => options.PdfParserMaxBytes > 0 && options.PdfParserMaxBytes <= options.AssignmentSubmissionMaxBytes &&
                options.PdfParserMaxPages is >= 1 and <= 10000,
                "Media PDF parser bounds are invalid.")
            .Validate(options => options.ProcessTimeout >= TimeSpan.FromSeconds(1) && options.ProcessTimeout <= TimeSpan.FromMinutes(30) &&
                options.ProcessOutputCharacterLimit is >= 4096 and <= 1024 * 1024,
                "Media process limits are invalid.")
            .ValidateOnStart();
        services.AddOptions<MediaStorageOptions>()
            .Bind(configuration.GetSection(MediaStorageOptions.SectionName))
            .Validate(options => !options.Enabled ||
                Uri.TryCreate(options.Endpoint, UriKind.Absolute, out Uri? endpoint) &&
                (endpoint.Scheme == Uri.UriSchemeHttps || endpoint.Host is "127.0.0.1" or "localhost" or "minio"),
                "Media object storage endpoint is invalid.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Bucket),
                "Media object storage bucket is required.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.AccessKey),
                "Media object storage access key is required.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.SecretKey),
                "Media object storage secret key is required.")
            .Validate(options => !options.Enabled || string.IsNullOrWhiteSpace(options.PublicEndpoint) ||
                Uri.TryCreate(options.PublicEndpoint, UriKind.Absolute, out _),
                "Media object storage public endpoint is invalid.")
            .Validate(options => options.UploadUrlMinutes is >= 1 and <= 10 && options.DownloadUrlMinutes is >= 1 and <= 5,
                "Media signed URL lifetimes are invalid.")
            .ValidateOnStart();
        services.AddOptions<CloudinaryOptions>()
            .Bind(configuration.GetSection(CloudinaryOptions.SectionName))
            .Validate(options => !options.Enabled || IsValidCloudinaryCloudName(options.CloudName),
                "Cloudinary cloud name is required and must contain only ASCII letters, digits, hyphens, or underscores when enabled.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ApiKey),
                "Cloudinary API key is required when enabled.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ApiSecret),
                "Cloudinary API secret is required when enabled.")
            .Validate(options => options.RequestTimeoutSeconds is >= 1 and <= 120 &&
                options.UploadTimeoutSeconds is >= 5 and <= 600,
                "Cloudinary request timeouts are invalid.")
            .ValidateOnStart();
        services.AddSingleton<IObjectStorage>(provider =>
        {
            MediaStorageOptions options = provider.GetRequiredService<IOptions<MediaStorageOptions>>().Value;
            return options.Enabled ? new S3ObjectStorage(options) : new DisabledObjectStorage();
        });
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

        services.ConfigureHttpClientDefaults(httpClient =>
            httpClient.AddStandardResilienceHandler(options =>
                options.Retry.DisableForUnsafeHttpMethods()));
        services.AddHttpClient<IProcessedImageStore, CloudinaryProcessedImageStore>(client =>
        {
            client.BaseAddress = new Uri("https://api.cloudinary.com/", UriKind.Absolute);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
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

    private static bool IsValidCloudinaryCloudName(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 255 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
