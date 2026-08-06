namespace Dorosak.Domain.Identity;

public sealed class RefreshSession
{
    private RefreshSession()
    {
    }

    private RefreshSession(
        Guid id,
        Guid userId,
        Guid familyId,
        DateTimeOffset createdAt,
        DateTimeOffset authenticatedAt,
        DateTimeOffset idleExpiresAt,
        DateTimeOffset absoluteExpiresAt,
        string deviceName,
        string ipAddressHash,
        string authenticationMethods,
        int authorizationVersion)
    {
        Id = id;
        UserId = userId;
        FamilyId = familyId;
        CreatedAt = createdAt;
        LastUsedAt = createdAt;
        AuthenticatedAt = authenticatedAt;
        IdleExpiresAt = idleExpiresAt;
        AbsoluteExpiresAt = absoluteExpiresAt;
        DeviceName = deviceName;
        IpAddressHash = ipAddressHash;
        AuthenticationMethods = authenticationMethods;
        AuthorizationVersion = authorizationVersion;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid FamilyId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastUsedAt { get; private set; }

    public DateTimeOffset AuthenticatedAt { get; private set; }

    public DateTimeOffset IdleExpiresAt { get; private set; }

    public DateTimeOffset AbsoluteExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevocationReason { get; private set; }

    public string DeviceName { get; private set; } = string.Empty;

    public string IpAddressHash { get; private set; } = string.Empty;

    public string AuthenticationMethods { get; private set; } = string.Empty;

    public int AuthorizationVersion { get; private set; }

    public static RefreshSession Create(
        Guid userId,
        DateTimeOffset now,
        TimeSpan idleLifetime,
        TimeSpan absoluteLifetime,
        string deviceName,
        string ipAddressHash,
        string authenticationMethods,
        int authorizationVersion)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleLifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(absoluteLifetime, TimeSpan.Zero);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddressHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationMethods);

        DateTimeOffset absoluteExpiresAt = now.Add(absoluteLifetime);
        DateTimeOffset idleExpiresAt = now.Add(idleLifetime);
        if (idleExpiresAt > absoluteExpiresAt)
        {
            idleExpiresAt = absoluteExpiresAt;
        }

        return new RefreshSession(
            Guid.CreateVersion7(),
            userId,
            Guid.CreateVersion7(),
            now,
            now,
            idleExpiresAt,
            absoluteExpiresAt,
            deviceName,
            ipAddressHash,
            authenticationMethods,
            authorizationVersion);
    }

    public bool IsActive(DateTimeOffset now) =>
        RevokedAt is null && now < IdleExpiresAt && now < AbsoluteExpiresAt;

    public void Touch(DateTimeOffset now, TimeSpan idleLifetime)
    {
        if (!IsActive(now))
        {
            return;
        }

        LastUsedAt = now;
        DateTimeOffset nextIdleExpiry = now.Add(idleLifetime);
        IdleExpiresAt = nextIdleExpiry < AbsoluteExpiresAt ? nextIdleExpiry : AbsoluteExpiresAt;
    }

    public void Revoke(DateTimeOffset now, string reason)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevocationReason = reason;
    }
}
