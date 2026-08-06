namespace Dorosak.Domain.Identity;

public sealed class MfaChallenge
{
    private MfaChallenge()
    {
    }

    private MfaChallenge(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public static MfaChallenge Create(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        return new MfaChallenge(Guid.CreateVersion7(), userId, tokenHash, now, now.Add(lifetime));
    }

    public bool IsActive(DateTimeOffset now, int maximumAttempts) =>
        ConsumedAt is null && AttemptCount < maximumAttempts && now < ExpiresAt;

    public void RegisterFailure() => AttemptCount++;

    public void Consume(DateTimeOffset now)
    {
        if (ConsumedAt is not null)
        {
            throw new InvalidOperationException("The MFA challenge has already been consumed.");
        }

        ConsumedAt = now;
    }
}
