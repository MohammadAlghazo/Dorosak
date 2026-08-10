using Dorosak.Domain.Common;

namespace Dorosak.Domain.Engagement;

public enum DiscussionStatus
{
    Published,
    Hidden,
    Removed,
}

public enum DiscussionCommentStatus
{
    Published,
    Hidden,
    Removed,
}

public sealed class DiscussionThread
{
    public const int EditWindowMinutes = 15;

    private DiscussionThread()
    {
    }

    private DiscussionThread(
        Guid id,
        Guid courseId,
        Guid releaseId,
        Guid? lessonId,
        Guid authorUserId,
        string title,
        string body,
        DateTimeOffset now)
    {
        Id = id;
        CourseId = courseId;
        ReleaseId = releaseId;
        LessonId = lessonId;
        AuthorUserId = authorUserId;
        Title = title;
        Body = body;
        Status = DiscussionStatus.Published;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid CourseId { get; private set; }

    public Guid ReleaseId { get; private set; }

    public Guid? LessonId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public DiscussionStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? EditedAt { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public static DiscussionThread Create(
        Guid courseId,
        Guid releaseId,
        Guid? lessonId,
        Guid authorUserId,
        string title,
        string body,
        DateTimeOffset now)
    {
        Validate(title, body);
        if (courseId == Guid.Empty || releaseId == Guid.Empty || authorUserId == Guid.Empty)
        {
            throw new DomainRuleException("DISCUSSION.IDENTITY_REQUIRED", "Discussion ownership identifiers are required.");
        }

        return new(
            Guid.CreateVersion7(),
            courseId,
            releaseId,
            lessonId,
            authorUserId,
            Normalize(title),
            Normalize(body),
            now);
    }

    public void Update(string title, string body, DateTimeOffset now)
    {
        EnsureEditable(now);
        Validate(title, body);
        Title = Normalize(title);
        Body = Normalize(body);
        EditedAt = now;
        UpdatedAt = now;
    }

    public bool Remove(DateTimeOffset now)
    {
        if (Status == DiscussionStatus.Removed)
        {
            return false;
        }

        if (Status != DiscussionStatus.Published)
        {
            throw new DomainRuleException("DISCUSSION.NOT_REMOVABLE", "The discussion cannot be removed in its current state.");
        }

        Status = DiscussionStatus.Removed;
        RemovedAt = now;
        UpdatedAt = now;
        return true;
    }

    private void EnsureEditable(DateTimeOffset now)
    {
        if (Status != DiscussionStatus.Published)
        {
            throw new DomainRuleException("DISCUSSION.NOT_EDITABLE", "The discussion cannot be changed in its current state.");
        }

        if (now > CreatedAt.AddMinutes(EditWindowMinutes))
        {
            throw new DomainRuleException("DISCUSSION.EDIT_WINDOW_CLOSED", "The discussion edit window has closed.");
        }
    }

    private static void Validate(string title, string body)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
        {
            throw new DomainRuleException("DISCUSSION.TITLE_INVALID", "A discussion title is required and cannot exceed 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 10000)
        {
            throw new DomainRuleException("DISCUSSION.BODY_INVALID", "A discussion body is required and cannot exceed 10000 characters.");
        }
    }

    private static string Normalize(string value) => value.Trim();
}

public sealed class DiscussionComment
{
    public const int MaximumDepth = 2;
    public const int EditWindowMinutes = 15;

    private DiscussionComment()
    {
    }

    private DiscussionComment(
        Guid id,
        Guid threadId,
        Guid? parentCommentId,
        Guid authorUserId,
        int depth,
        string body,
        DateTimeOffset now)
    {
        Id = id;
        ThreadId = threadId;
        ParentCommentId = parentCommentId;
        ParentDepth = parentCommentId is null ? null : depth - 1;
        AuthorUserId = authorUserId;
        Depth = depth;
        Body = body;
        Status = DiscussionCommentStatus.Published;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid ThreadId { get; private set; }

    public Guid? ParentCommentId { get; private set; }

    public int? ParentDepth { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public int Depth { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DiscussionCommentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? EditedAt { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public static DiscussionComment Create(
        Guid threadId,
        Guid? parentCommentId,
        int parentDepth,
        Guid authorUserId,
        string body,
        DateTimeOffset now)
    {
        if (threadId == Guid.Empty || authorUserId == Guid.Empty)
        {
            throw new DomainRuleException("COMMENT.IDENTITY_REQUIRED", "Comment ownership identifiers are required.");
        }

        if (parentCommentId is null && parentDepth != -1 || parentCommentId is not null && parentDepth < 0)
        {
            throw new DomainRuleException("COMMENT.PARENT_INVALID", "The comment parent context is invalid.");
        }

        int depth = parentCommentId is null ? 0 : parentDepth + 1;
        if (depth > MaximumDepth)
        {
            throw new DomainRuleException("COMMENT.DEPTH_LIMIT", "Replies cannot be nested beyond two levels.");
        }

        ValidateBody(body);
        return new(Guid.CreateVersion7(), threadId, parentCommentId, authorUserId, depth, body.Trim(), now);
    }

    public void Update(string body, DateTimeOffset now)
    {
        EnsureEditable(now);
        ValidateBody(body);
        Body = body.Trim();
        EditedAt = now;
        UpdatedAt = now;
    }

    public bool Remove(DateTimeOffset now)
    {
        if (Status == DiscussionCommentStatus.Removed)
        {
            return false;
        }

        if (Status != DiscussionCommentStatus.Published)
        {
            throw new DomainRuleException("COMMENT.NOT_REMOVABLE", "The comment cannot be removed in its current state.");
        }

        Status = DiscussionCommentStatus.Removed;
        RemovedAt = now;
        UpdatedAt = now;
        return true;
    }

    private void EnsureEditable(DateTimeOffset now)
    {
        if (Status != DiscussionCommentStatus.Published)
        {
            throw new DomainRuleException("COMMENT.NOT_EDITABLE", "The comment cannot be changed in its current state.");
        }

        if (now > CreatedAt.AddMinutes(EditWindowMinutes))
        {
            throw new DomainRuleException("COMMENT.EDIT_WINDOW_CLOSED", "The comment edit window has closed.");
        }
    }

    private static void ValidateBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 5000)
        {
            throw new DomainRuleException("COMMENT.BODY_INVALID", "A comment is required and cannot exceed 5000 characters.");
        }
    }
}

public sealed class CommentLike
{
    private CommentLike()
    {
    }

    private CommentLike(Guid commentId, Guid userId, DateTimeOffset createdAt)
    {
        CommentId = commentId;
        UserId = userId;
        CreatedAt = createdAt;
    }

    public Guid CommentId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static CommentLike Create(Guid commentId, Guid userId, DateTimeOffset now)
    {
        if (commentId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainRuleException("COMMENT_LIKE.IDENTITY_REQUIRED", "Like ownership identifiers are required.");
        }

        return new(commentId, userId, now);
    }
}
