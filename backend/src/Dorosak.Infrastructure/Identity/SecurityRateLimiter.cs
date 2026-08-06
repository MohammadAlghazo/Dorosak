using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dorosak.Infrastructure.Identity;

internal sealed class SecurityRateLimiter(
    IConnectionMultiplexer redis,
    IOptions<SecurityRateLimitOptions> options,
    ILogger<SecurityRateLimiter> logger)
{
    private const string IncrementScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        local ttl = redis.call('PTTL', KEYS[1])
        return { count, ttl }
        """;

    private static readonly Action<ILogger, string, Exception?> StoreUnavailable = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(5100, nameof(StoreUnavailable)),
        "Security rate-limit store failed for operation {Operation}");

    private readonly SecurityRateLimitOptions _options = options.Value;

    public async Task<SecurityRateLimitResult> CheckAsync(
        string operation,
        string partition,
        int permitLimit,
        TimeSpan window)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(partition);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(permitLimit);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);

        string partitionHash = HashPartition(partition);
        string key = $"{_options.KeyPrefix}:{operation}:{partitionHash}";

        try
        {
            IDatabase database = redis.GetDatabase();
            RedisResult result = await database.ScriptEvaluateAsync(
                IncrementScript,
                [key],
                [Math.Max(1L, (long)Math.Ceiling(window.TotalMilliseconds))]);
            RedisResult[] values = (RedisResult[]?)result ?? [];
            long count = values.Length > 0 ? (long)values[0] : permitLimit + 1L;
            long ttlMilliseconds = values.Length > 1 ? Math.Max(1000L, (long)values[1]) : 1000L;
            return count <= permitLimit
                ? SecurityRateLimitResult.AllowedResult
                : SecurityRateLimitResult.Rejected(TimeSpan.FromMilliseconds(ttlMilliseconds));
        }
        catch (RedisException exception)
        {
            StoreUnavailable(logger, operation, exception);
            return SecurityRateLimitResult.Unavailable(TimeSpan.FromSeconds(30));
        }
    }

    private string HashPartition(string partition)
    {
        byte[] salt = Encoding.UTF8.GetBytes(_options.PartitionSalt);
        byte[] value = Encoding.UTF8.GetBytes(partition.Trim().ToUpperInvariant());
        byte[] hash = HMACSHA256.HashData(salt, value);
        return Convert.ToHexString(hash).ToLower(CultureInfo.InvariantCulture);
    }
}

internal sealed record SecurityRateLimitResult(bool IsAllowed, bool IsAvailable, TimeSpan? RetryAfter)
{
    public static readonly SecurityRateLimitResult AllowedResult = new(true, true, null);

    public static SecurityRateLimitResult Rejected(TimeSpan retryAfter) => new(false, true, retryAfter);

    public static SecurityRateLimitResult Unavailable(TimeSpan retryAfter) => new(false, false, retryAfter);
}
