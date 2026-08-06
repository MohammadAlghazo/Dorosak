namespace Dorosak.Domain.Identity;

public sealed class SecurityEvent
{
    private SecurityEvent()
    {
    }

    private SecurityEvent(
        Guid id,
        Guid? userId,
        Guid? sessionId,
        string type,
        DateTimeOffset occurredAt,
        string? ipAddressHash,
        string? metadata)
    {
        Id = id;
        UserId = userId;
        SessionId = sessionId;
        Type = type;
        OccurredAt = occurredAt;
        IpAddressHash = ipAddressHash;
        Metadata = metadata;
    }

    public Guid Id { get; private set; }

    public Guid? UserId { get; private set; }

    public Guid? SessionId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public string? IpAddressHash { get; private set; }

    public string? Metadata { get; private set; }

    public static SecurityEvent Create(
        Guid? userId,
        Guid? sessionId,
        string type,
        DateTimeOffset occurredAt,
        string? ipAddressHash = null,
        string? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        return new SecurityEvent(Guid.CreateVersion7(), userId, sessionId, type, occurredAt, ipAddressHash, metadata);
    }
}
