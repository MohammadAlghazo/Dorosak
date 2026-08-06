using System.Text.Json;

namespace Dorosak.Domain.Operations;

public sealed class IdempotencyRecord
{
    private IdempotencyRecord()
    {
    }

    private IdempotencyRecord(
        Guid id,
        string scope,
        string operation,
        string key,
        string requestHash,
        string responsePayload,
        int responseSchemaVersion,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        Scope = scope;
        Operation = operation;
        Key = key;
        RequestHash = requestHash;
        ResponsePayload = responsePayload;
        ResponseSchemaVersion = responseSchemaVersion;
        CreatedAt = createdAt;
        CompletedAt = createdAt;
        ExpiresAt = expiresAt;
        Status = IdempotencyStatus.Completed;
    }

    public Guid Id { get; private set; }

    public string Scope { get; private set; } = string.Empty;

    public string Operation { get; private set; } = string.Empty;

    public string Key { get; private set; } = string.Empty;

    public string RequestHash { get; private set; } = string.Empty;

    public IdempotencyStatus Status { get; private set; }

    public string ResponsePayload { get; private set; } = string.Empty;

    public int ResponseSchemaVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset CompletedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public static IdempotencyRecord CreateCompleted(
        string scope,
        string operation,
        string key,
        string requestHash,
        string responsePayload,
        int responseSchemaVersion,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(responsePayload);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(responseSchemaVersion);

        if (scope.Length > 200)
        {
            throw new ArgumentException("Scope cannot exceed 200 characters.", nameof(scope));
        }
        if (operation.Length > 400)
        {
            throw new ArgumentException("Operation cannot exceed 400 characters.", nameof(operation));
        }
        if (key.Length > 200)
        {
            throw new ArgumentException("Key cannot exceed 200 characters.", nameof(key));
        }
        if (requestHash.Length != 64 || !requestHash.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Request hash must be a 64-character hexadecimal SHA-256 value.", nameof(requestHash));
        }
        if (createdAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Creation time must use the UTC offset.", nameof(createdAt));
        }
        if (expiresAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Expiration time must use the UTC offset.", nameof(expiresAt));
        }
        if (expiresAt <= createdAt)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Expiration must be after creation.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(responsePayload);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Response payload must contain valid JSON.", nameof(responsePayload), exception);
        }

        return new IdempotencyRecord(
            Guid.CreateVersion7(),
            scope,
            operation,
            key,
            requestHash,
            responsePayload,
            responseSchemaVersion,
            createdAt,
            expiresAt);
    }
}
