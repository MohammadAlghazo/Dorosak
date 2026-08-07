using System.Text.Json;

namespace Dorosak.Domain.Operations;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    private OutboxMessage(
        Guid id,
        DateTimeOffset occurredAt,
        DateTimeOffset availableAt,
        string eventType,
        int schemaVersion,
        string payload,
        string headers)
    {
        Id = id;
        OccurredAt = occurredAt;
        AvailableAt = availableAt;
        EventType = eventType;
        SchemaVersion = schemaVersion;
        Payload = payload;
        Headers = headers;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset AvailableAt { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public int SchemaVersion { get; private set; }

    public string Payload { get; private set; } = "{}";

    public string Headers { get; private set; } = "{}";

    public int AttemptCount { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public Guid? LockToken { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public string? LastErrorCode { get; private set; }

    public static OutboxMessage Create(
        string eventType,
        int schemaVersion,
        string payload,
        string headers,
        DateTimeOffset occurredAt,
        DateTimeOffset? availableAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(headers);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schemaVersion);

        if (eventType.Length > 300)
        {
            throw new ArgumentException("Event type cannot exceed 300 characters.", nameof(eventType));
        }
        if (occurredAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Occurrence time must use the UTC offset.", nameof(occurredAt));
        }

        DateTimeOffset effectiveAvailableAt = availableAt ?? occurredAt;
        if (effectiveAvailableAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Availability time must use the UTC offset.", nameof(availableAt));
        }
        if (effectiveAvailableAt < occurredAt)
        {
            throw new ArgumentOutOfRangeException(nameof(availableAt), "Availability cannot precede occurrence.");
        }

        EnsureValidJson(payload, nameof(payload));
        EnsureValidJson(headers, nameof(headers));

        return new OutboxMessage(
            Guid.CreateVersion7(),
            occurredAt,
            effectiveAvailableAt,
            eventType,
            schemaVersion,
            payload,
            headers);
    }

    public bool TryAcquire(DateTimeOffset now, TimeSpan lockDuration, Guid lockToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lockDuration, TimeSpan.Zero);
        if (ProcessedAt is not null || AvailableAt > now || LockedUntil > now)
        {
            return false;
        }

        AttemptCount++;
        LockToken = lockToken;
        LockedUntil = now.Add(lockDuration);
        return true;
    }

    public void MarkProcessed(DateTimeOffset now, Guid lockToken)
    {
        EnsureLock(lockToken);
        ProcessedAt = now;
        LockedUntil = null;
        LockToken = null;
        LastErrorCode = null;
    }

    public void ReleaseAfterFailure(DateTimeOffset now, Guid lockToken, string errorCode, TimeSpan retryDelay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retryDelay, TimeSpan.Zero);
        EnsureLock(lockToken);
        AvailableAt = now.Add(retryDelay);
        LastErrorCode = errorCode.Length <= 200 ? errorCode : errorCode[..200];
        LockedUntil = null;
        LockToken = null;
    }

    private void EnsureLock(Guid lockToken)
    {
        if (LockToken != lockToken)
        {
            throw new InvalidOperationException("The outbox message is not owned by this worker lock.");
        }
    }

    private static void EnsureValidJson(string value, string parameterName)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(value);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Value must contain valid JSON.", parameterName, exception);
        }
    }
}
