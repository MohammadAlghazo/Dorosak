namespace Dorosak.Infrastructure.Identity;

public sealed class IdentitySecurityOptions
{
    public const string SectionName = "Identity";

    public int AccessTokenMinutes { get; init; } = 10;

    public int RefreshIdleDays { get; init; } = 14;

    public int RefreshAbsoluteDays { get; init; } = 30;

    public int RefreshRaceWindowSeconds { get; init; } = 10;

    public int RecentAuthenticationMinutes { get; init; } = 15;

    public int MfaChallengeMinutes { get; init; } = 5;

    public int MfaMaximumAttempts { get; init; } = 5;

    public int RecoveryCodeCount { get; init; } = 10;

    public int PasswordHashIterations { get; init; } = 210000;

    public string RefreshCookieName { get; init; } = "__Secure-dorosak-refresh";
}
