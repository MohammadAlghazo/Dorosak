namespace Dorosak.Infrastructure.Identity;

public sealed class SecurityRateLimitOptions
{
    public const string SectionName = "SecurityRateLimits";

    public string KeyPrefix { get; init; } = "dorosak:development:v1:security-rate-limit";

    public string PartitionSalt { get; init; } = "dorosak-development-rate-limit-partition";
}
