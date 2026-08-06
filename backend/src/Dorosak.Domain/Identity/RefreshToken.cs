namespace Dorosak.Domain.Identity;

public sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    private RefreshToken(
        Guid id,
        Guid sessionId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        SessionId = sessionId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevocationReason { get; private set; }

    public static RefreshToken Create(
        Guid sessionId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset now,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        if (expiresAt <= now)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Token expiry must be in the future.");
        }

        return new RefreshToken(Guid.CreateVersion7(), sessionId, familyId, tokenHash, now, expiresAt);
    }

    public bool IsActive(DateTimeOffset now) =>
        ConsumedAt is null && RevokedAt is null && now < ExpiresAt;

    public bool WasConsumedRecently(DateTimeOffset now, TimeSpan raceWindow) =>
        ConsumedAt is { } consumedAt && now - consumedAt <= raceWindow;

    public void Consume(DateTimeOffset now, Guid replacementId)
    {
        if (ConsumedAt is not null || RevokedAt is not null)
        {
            throw new InvalidOperationException("Only an active refresh token can be consumed.");
        }

        ConsumedAt = now;
        ReplacedByTokenId = replacementId;
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
