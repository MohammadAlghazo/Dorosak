namespace Dorosak.Application.Features.Identity;

public sealed record IdentityRequestContext(string IpAddress, string UserAgent, string Locale);

public sealed record RegistrationAcceptedResponse(bool Accepted);

public sealed record NeutralAcceptedResponse(bool Accepted);

public sealed record OperationCompletedResponse(bool Completed);

public sealed record SignInResponse(
    string Outcome,
    AuthenticatedSessionResponse? Session,
    string? ChallengeToken,
    DateTimeOffset? ChallengeExpiresAt);

public sealed record AuthenticatedSessionResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    IdentitySnapshotResponse Identity);

public sealed record IdentitySnapshotResponse(
    Guid UserId,
    Guid SessionId,
    string DisplayName,
    string Email,
    bool EmailVerified,
    bool MfaEnabled,
    DateTimeOffset AuthenticatedAt,
    DateTimeOffset RecentAuthenticationExpiresAt,
    int AuthorizationVersion,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> AuthenticationMethods);

public sealed record SessionSummaryResponse(
    Guid SessionId,
    bool IsCurrent,
    string DeviceName,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt,
    DateTimeOffset IdleExpiresAt,
    DateTimeOffset AbsoluteExpiresAt);

public sealed record SessionsResponse(IReadOnlyList<SessionSummaryResponse> Sessions);

public sealed record MfaSetupResponse(string Secret, string OtpAuthUri);

public sealed record MfaConfirmationResponse(IReadOnlyList<string> RecoveryCodes);
