namespace Dorosak.Domain.Identity;

public sealed class MfaRecoveryCode
{
    private MfaRecoveryCode()
    {
    }

    private MfaRecoveryCode(Guid id, Guid userId, string codeHash, DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        CodeHash = codeHash;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string CodeHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public static MfaRecoveryCode Create(Guid userId, string codeHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);
        return new MfaRecoveryCode(Guid.CreateVersion7(), userId, codeHash, now);
    }

    public bool IsAvailable => UsedAt is null;

    public void Use(DateTimeOffset now)
    {
        if (UsedAt is not null)
        {
            throw new InvalidOperationException("The recovery code has already been used.");
        }

        UsedAt = now;
    }
}
