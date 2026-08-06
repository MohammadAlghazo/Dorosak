namespace Dorosak.Domain.Profiles;

public sealed class UserProfile
{
    private UserProfile()
    {
    }

    private UserProfile(Guid userId, string displayName, DateTimeOffset now)
    {
        UserId = userId;
        DisplayName = displayName;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid UserId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserProfile Create(Guid userId, string displayName, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return new UserProfile(userId, displayName.Trim(), now);
    }

    public void Rename(string displayName, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
        UpdatedAt = now;
    }
}
