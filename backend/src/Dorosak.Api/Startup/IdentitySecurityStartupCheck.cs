using Dorosak.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace Dorosak.Api.Startup;

public sealed class IdentitySecurityStartupCheck(
    IHostEnvironment environment,
    IJwtKeyProvider jwtKeyProvider,
    IOptions<SecurityRateLimitOptions> rateLimitOptions,
    IOptions<PasswordBreachOptions> passwordBreachOptions,
    IOptions<ApplicationOptions> applicationOptions) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = jwtKeyProvider.ValidationKey;
        if (!environment.IsProduction())
        {
            return Task.CompletedTask;
        }

        if (rateLimitOptions.Value.PartitionSalt.Length < 32 ||
            rateLimitOptions.Value.PartitionSalt.Contains("development", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A production security rate-limit partition secret is required.");
        }
        if (!passwordBreachOptions.Value.Enabled)
        {
            throw new InvalidOperationException("Breached-password checks must be enabled in Production.");
        }
        if (!Uri.TryCreate(applicationOptions.Value.PublicUrl, UriKind.Absolute, out Uri? publicUrl) ||
            publicUrl.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("App:PublicUrl must use HTTPS in Production.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
