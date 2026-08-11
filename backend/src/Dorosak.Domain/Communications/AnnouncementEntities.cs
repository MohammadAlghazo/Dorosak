using Dorosak.Domain.Common;

namespace Dorosak.Domain.Communications;

public sealed class Announcement
{
    public const int MaximumTitleLength = 200;

    public const int MaximumBodyLength = 10000;

    private Announcement()
    {
    }

    private Announcement(
        Guid id,
        Guid courseId,
        Guid createdByUserId,
        string title,
        string body,
        DateTimeOffset now)
    {
        Id = id;
        CourseId = courseId;
        CreatedByUserId = createdByUserId;
        Title = title;
        Body = body;
        Version = 1;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid CourseId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public long Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedByUserId { get; private set; }

    public static Announcement Create(
        Guid courseId,
        Guid createdByUserId,
        string title,
        string body,
        DateTimeOffset now)
    {
        if (courseId == Guid.Empty || createdByUserId == Guid.Empty)
        {
            throw new DomainRuleException("ANNOUNCEMENT.IDENTITY_REQUIRED", "Announcement identifiers are required.");
        }

        (string normalizedTitle, string normalizedBody) = NormalizeContent(title, body);
        EnsureUtc(now);
        return new Announcement(Guid.CreateVersion7(), courseId, createdByUserId, normalizedTitle, normalizedBody, now);
    }

    public bool Update(string title, string body, DateTimeOffset now)
    {
        (string normalizedTitle, string normalizedBody) = NormalizeContent(title, body);
        EnsureUtc(now);
        if (DeletedAt is not null)
        {
            throw new DomainRuleException("ANNOUNCEMENT.DELETED", "A deleted announcement cannot be updated.");
        }
        if (now < CreatedAt)
        {
            throw new DomainRuleException("ANNOUNCEMENT.TIME_INVALID", "An announcement update cannot precede creation.");
        }
        if (string.Equals(Title, normalizedTitle, StringComparison.Ordinal) &&
            string.Equals(Body, normalizedBody, StringComparison.Ordinal))
        {
            return false;
        }

        if (Version == long.MaxValue)
        {
            throw new DomainRuleException("ANNOUNCEMENT.VERSION_EXHAUSTED", "The announcement version has been exhausted.");
        }

        Title = normalizedTitle;
        Body = normalizedBody;
        Version++;
        UpdatedAt = now;
        return true;
    }

    public bool Delete(Guid actorUserId, DateTimeOffset now)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new DomainRuleException("ANNOUNCEMENT.ACTOR_REQUIRED", "An announcement deletion actor is required.");
        }
        EnsureUtc(now);
        if (now < CreatedAt)
        {
            throw new DomainRuleException("ANNOUNCEMENT.TIME_INVALID", "An announcement deletion cannot precede creation.");
        }
        if (DeletedAt is not null)
        {
            return false;
        }
        if (Version == long.MaxValue)
        {
            throw new DomainRuleException("ANNOUNCEMENT.VERSION_EXHAUSTED", "The announcement version has been exhausted.");
        }

        DeletedAt = now;
        DeletedByUserId = actorUserId;
        Version++;
        UpdatedAt = now;
        return true;
    }

    private static (string Title, string Body) NormalizeContent(string title, string body)
    {
        string normalizedTitle = title?.Trim() ?? string.Empty;
        string normalizedBody = body?.Trim() ?? string.Empty;
        if (normalizedTitle.Length is 0 or > MaximumTitleLength)
        {
            throw new DomainRuleException(
                "ANNOUNCEMENT.TITLE_INVALID",
                $"An announcement title is required and cannot exceed {MaximumTitleLength} characters.");
        }
        if (normalizedBody.Length is 0 or > MaximumBodyLength)
        {
            throw new DomainRuleException(
                "ANNOUNCEMENT.BODY_INVALID",
                $"An announcement body is required and cannot exceed {MaximumBodyLength} characters.");
        }

        return (normalizedTitle, normalizedBody);
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("ANNOUNCEMENT.UTC_REQUIRED", "Announcement timestamps must use UTC.");
        }
    }
}

public sealed class AnnouncementTarget
{
    private AnnouncementTarget()
    {
    }

    private AnnouncementTarget(
        Guid announcementId,
        Guid userId,
        long announcementVersion,
        Guid notificationId,
        DateTimeOffset createdAt)
    {
        AnnouncementId = announcementId;
        UserId = userId;
        AnnouncementVersion = announcementVersion;
        NotificationId = notificationId;
        CreatedAt = createdAt;
    }

    public Guid AnnouncementId { get; private set; }

    public Guid UserId { get; private set; }

    public long AnnouncementVersion { get; private set; }

    public Guid NotificationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static AnnouncementTarget Create(
        Guid announcementId,
        Guid userId,
        long announcementVersion,
        Guid notificationId,
        DateTimeOffset now)
    {
        if (announcementId == Guid.Empty || userId == Guid.Empty || notificationId == Guid.Empty || announcementVersion <= 0)
        {
            throw new DomainRuleException("ANNOUNCEMENT_TARGET.INVALID", "Announcement target identifiers are invalid.");
        }
        if (now.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("ANNOUNCEMENT_TARGET.UTC_REQUIRED", "Announcement target timestamps must use UTC.");
        }

        return new AnnouncementTarget(announcementId, userId, announcementVersion, notificationId, now);
    }
}
