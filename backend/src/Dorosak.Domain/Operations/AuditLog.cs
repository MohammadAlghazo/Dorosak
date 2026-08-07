namespace Dorosak.Domain.Operations;

public sealed class AuditLog
{
    private AuditLog()
    {
    }

    private AuditLog(
        Guid id,
        Guid actorUserId,
        string action,
        string targetType,
        Guid targetId,
        string result,
        string? reason,
        DateTimeOffset occurredAt)
    {
        Id = id;
        ActorUserId = actorUserId;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        Result = result;
        Reason = reason;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string TargetType { get; private set; } = string.Empty;

    public Guid TargetId { get; private set; }

    public string Result { get; private set; } = string.Empty;

    public string? Reason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static AuditLog Create(
        Guid actorUserId,
        string action,
        string targetType,
        Guid targetId,
        string result,
        string? reason,
        DateTimeOffset occurredAt) => new(
            Guid.CreateVersion7(),
            actorUserId,
            action,
            targetType,
            targetId,
            result,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            occurredAt);
}
