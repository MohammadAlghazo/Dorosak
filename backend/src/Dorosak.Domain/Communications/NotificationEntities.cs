using Dorosak.Domain.Common;

namespace Dorosak.Domain.Communications;

public sealed class NotificationSequence
{
    private NotificationSequence()
    {
    }

    private NotificationSequence(Guid userId)
    {
        UserId = userId;
    }

    public Guid UserId { get; private set; }

    public long LastSequence { get; private set; }

    public static NotificationSequence Create(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainRuleException(
                "NOTIFICATION_SEQUENCE.IDENTITY_REQUIRED",
                "A notification sequence owner is required.");
        }

        return new NotificationSequence(userId);
    }

    public long Advance()
    {
        if (LastSequence == long.MaxValue)
        {
            throw new DomainRuleException(
                "NOTIFICATION_SEQUENCE.EXHAUSTED",
                "The notification sequence has been exhausted.");
        }

        LastSequence++;
        return LastSequence;
    }
}

public sealed class Notification
{
    private Notification()
    {
    }

    private Notification(
        Guid id,
        Guid userId,
        long sequence,
        Guid? messageId,
        Guid? announcementId,
        long? announcementVersion,
        Guid targetAnnouncementId,
        long targetAnnouncementVersion,
        string? title,
        string? body,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        Sequence = sequence;
        MessageId = messageId;
        AnnouncementId = announcementId;
        AnnouncementVersion = announcementVersion;
        TargetAnnouncementId = targetAnnouncementId;
        TargetAnnouncementVersion = targetAnnouncementVersion;
        Title = title;
        Body = body;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public long Sequence { get; private set; }

    public Guid? MessageId { get; private set; }

    public Guid? AnnouncementId { get; private set; }

    public long? AnnouncementVersion { get; private set; }

    public Guid TargetAnnouncementId { get; private set; }

    public long TargetAnnouncementVersion { get; private set; }

    public string? Title { get; private set; }

    public string? Body { get; private set; }

    public bool IsRead { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Notification CreateForMessage(
        Guid userId,
        Guid messageId,
        long sequence,
        DateTimeOffset now)
    {
        EnsureIdentity(userId, messageId, sequence);
        EnsureUtc(now);
        return new Notification(
            Guid.CreateVersion7(),
            userId,
            sequence,
            messageId,
            null,
            null,
            Guid.Empty,
            0,
            null,
            null,
            now);
    }

    public static Notification CreateForAnnouncement(
        Guid userId,
        Guid announcementId,
        long announcementVersion,
        long sequence,
        string title,
        string body,
        DateTimeOffset now)
    {
        EnsureIdentity(userId, announcementId, sequence);
        if (announcementVersion <= 0)
        {
            throw new DomainRuleException(
                "NOTIFICATION.ANNOUNCEMENT_VERSION_INVALID",
                "An announcement notification version must be positive.");
        }

        string normalizedTitle = title?.Trim() ?? string.Empty;
        string normalizedBody = body?.Trim() ?? string.Empty;
        if (normalizedTitle.Length is 0 or > Announcement.MaximumTitleLength ||
            normalizedBody.Length is 0 or > Announcement.MaximumBodyLength)
        {
            throw new DomainRuleException(
                "NOTIFICATION.CONTENT_INVALID",
                "Announcement notification content is outside its bounds.");
        }

        EnsureUtc(now);
        return new Notification(
            Guid.CreateVersion7(),
            userId,
            sequence,
            null,
            announcementId,
            announcementVersion,
            announcementId,
            announcementVersion,
            normalizedTitle,
            normalizedBody,
            now);
    }

    public bool MarkRead(DateTimeOffset now)
    {
        EnsureUtc(now);
        if (now < CreatedAt)
        {
            throw new DomainRuleException("NOTIFICATION.READ_TIME_INVALID", "A read time cannot precede notification creation.");
        }
        if (IsRead)
        {
            return false;
        }

        IsRead = true;
        ReadAt = now;
        return true;
    }

    private static void EnsureIdentity(Guid userId, Guid resourceId, long sequence)
    {
        if (userId == Guid.Empty || resourceId == Guid.Empty)
        {
            throw new DomainRuleException(
                "NOTIFICATION.IDENTITY_REQUIRED",
                "Notification ownership and resource identifiers are required.");
        }
        if (sequence <= 0)
        {
            throw new DomainRuleException(
                "NOTIFICATION.SEQUENCE_INVALID",
                "A notification sequence must be positive.");
        }
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("NOTIFICATION.UTC_REQUIRED", "Notification timestamps must use UTC.");
        }
    }
}
