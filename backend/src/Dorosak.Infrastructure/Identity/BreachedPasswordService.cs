using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dorosak.Infrastructure.Identity;

internal sealed class BreachedPasswordService(
    HttpClient httpClient,
    IOptions<PasswordBreachOptions> options,
    ILogger<BreachedPasswordService> logger)
{
    private static readonly Action<ILogger, Exception?> LookupFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(5101, nameof(LookupFailed)),
        "The breached-password lookup failed");

    private readonly PasswordBreachOptions _options = options.Value;

    public async Task<PasswordBreachResult> CheckAsync(string password, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return PasswordBreachResult.Safe;
        }

        // HIBP's k-anonymity range protocol requires the SHA-1 password digest.
#pragma warning disable CA5350
        byte[] digest = SHA1.HashData(Encoding.UTF8.GetBytes(password));
#pragma warning restore CA5350
        string hash = Convert.ToHexString(digest);
        string prefix = hash[..5];
        string suffix = hash[5..];

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(
                $"range/{prefix}",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            foreach (string line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int separator = line.IndexOf(':', StringComparison.Ordinal);
                if (separator <= 0)
                {
                    continue;
                }

                if (string.Equals(line[..separator], suffix, StringComparison.OrdinalIgnoreCase))
                {
                    string countText = line[(separator + 1)..].Trim();
                    _ = int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out int count);
                    return new PasswordBreachResult(true, true, count);
                }
            }

            return PasswordBreachResult.Safe;
        }
        catch (HttpRequestException exception)
        {
            LookupFailed(logger, exception);
            return PasswordBreachResult.Unavailable;
        }
    }
}

internal sealed record PasswordBreachResult(bool IsAvailable, bool IsBreached, int OccurrenceCount)
{
    public static readonly PasswordBreachResult Safe = new(true, false, 0);

    public static readonly PasswordBreachResult Unavailable = new(false, false, 0);
}
