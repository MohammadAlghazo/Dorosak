using Dorosak.Domain.Common;

namespace Dorosak.Domain.Engagement;

public enum ContentReportReason
{
    Spam,
    Harassment,
    HateSpeech,
    Misinformation,
    Copyright,
    PersonalData,
    Other,
}

public enum ContentReportStatus
{
    Open,
    InReview,
    Resolved,
    Dismissed,
}

public enum ModerationCaseStatus
{
    Open,
    InReview,
    Resolved,
    Dismissed,
}

public enum ModerationActionType
{
    StartReview,
    HideContent,
    RestoreContent,
    Resolve,
    Dismiss,
}

public sealed record MessageReportSnapshot(
    Guid SenderUserId,
    string SenderName,
    Guid CourseId,
    string CourseTitle,
    Guid ConversationId,
    long Sequence,
    string Body,
    DateTimeOffset CreatedAt);

public sealed class ContentReport
{
    private ContentReport()
    {
    }

    private ContentReport(
        Guid id,
        Guid reporterUserId,
        Guid? courseId,
        Guid? reviewId,
        Guid? commentId,
        Guid? reportedUserId,
        Guid? messageId,
        ContentReportReason reason,
        string details,
        DateTimeOffset now,
        MessageReportSnapshot? messageSnapshot)
    {
        Id = id;
        ReporterUserId = reporterUserId;
        CourseId = courseId;
        ReviewId = reviewId;
        CommentId = commentId;
        ReportedUserId = reportedUserId;
        MessageId = messageId;
        MessageBodySnapshot = messageSnapshot?.Body;
        MessageSenderUserIdSnapshot = messageSnapshot?.SenderUserId;
        MessageSenderNameSnapshot = messageSnapshot?.SenderName;
        MessageCourseIdSnapshot = messageSnapshot?.CourseId;
        MessageCourseTitleSnapshot = messageSnapshot?.CourseTitle;
        MessageConversationIdSnapshot = messageSnapshot?.ConversationId;
        MessageSequenceSnapshot = messageSnapshot?.Sequence;
        MessageCreatedAtSnapshot = messageSnapshot?.CreatedAt;
        Reason = reason;
        Details = details;
        Status = ContentReportStatus.Open;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid ReporterUserId { get; private set; }

    public Guid? CourseId { get; private set; }

    public Guid? ReviewId { get; private set; }

    public Guid? CommentId { get; private set; }

    public Guid? ReportedUserId { get; private set; }

    public Guid? MessageId { get; private set; }

    public string? MessageBodySnapshot { get; private set; }

    public Guid? MessageSenderUserIdSnapshot { get; private set; }

    public string? MessageSenderNameSnapshot { get; private set; }

    public Guid? MessageCourseIdSnapshot { get; private set; }

    public string? MessageCourseTitleSnapshot { get; private set; }

    public Guid? MessageConversationIdSnapshot { get; private set; }

    public long? MessageSequenceSnapshot { get; private set; }

    public DateTimeOffset? MessageCreatedAtSnapshot { get; private set; }

    public ContentReportReason Reason { get; private set; }

    public string Details { get; private set; } = string.Empty;

    public ContentReportStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public static ContentReport Create(
        Guid reporterUserId,
        Guid? courseId,
        Guid? reviewId,
        Guid? commentId,
        Guid? reportedUserId,
        Guid? messageId,
        ContentReportReason reason,
        string? details,
        DateTimeOffset now,
        MessageReportSnapshot? messageSnapshot = null)
    {
        if (reporterUserId == Guid.Empty)
        {
            throw new DomainRuleException("REPORT.REPORTER_REQUIRED", "A report owner is required.");
        }

        Guid?[] targets = [courseId, reviewId, commentId, reportedUserId, messageId];
        if (targets.Count(target => target is not null) != 1 || targets.Any(target => target == Guid.Empty))
        {
            throw new DomainRuleException("REPORT.TARGET_INVALID", "A report must identify exactly one concrete target.");
        }

        if (messageId is null && messageSnapshot is not null)
        {
            throw new DomainRuleException("REPORT.MESSAGE_SNAPSHOT_INVALID", "A message snapshot requires a message target.");
        }

        if (messageSnapshot is not null)
        {
            if (messageSnapshot.SenderUserId == Guid.Empty || messageSnapshot.CourseId == Guid.Empty ||
                messageSnapshot.ConversationId == Guid.Empty || messageSnapshot.Sequence <= 0 ||
                string.IsNullOrWhiteSpace(messageSnapshot.Body) || messageSnapshot.Body.Length > 5000 ||
                string.IsNullOrWhiteSpace(messageSnapshot.SenderName) || messageSnapshot.SenderName.Length > 100 ||
                string.IsNullOrWhiteSpace(messageSnapshot.CourseTitle) || messageSnapshot.CourseTitle.Length > 200 ||
                messageSnapshot.CreatedAt.Offset != TimeSpan.Zero)
            {
                throw new DomainRuleException("REPORT.MESSAGE_SNAPSHOT_INVALID", "The message report snapshot is invalid.");
            }
        }

        if (reportedUserId == reporterUserId)
        {
            throw new DomainRuleException("REPORT.SELF_REPORT_INVALID", "An account cannot report itself.");
        }

        if (messageSnapshot?.SenderUserId == reporterUserId)
        {
            throw new DomainRuleException("REPORT.SELF_REPORT_INVALID", "An account cannot report its own message.");
        }

        string normalizedDetails = details?.Trim() ?? string.Empty;
        if (normalizedDetails.Length > 2000 || reason == ContentReportReason.Other && normalizedDetails.Length < 10)
        {
            throw new DomainRuleException(
                "REPORT.DETAILS_INVALID",
                "Report details cannot exceed 2000 characters and are required when the reason is Other.");
        }

        return new ContentReport(
            Guid.CreateVersion7(),
            reporterUserId,
                courseId,
                reviewId,
                commentId,
                reportedUserId,
                messageId,
                reason,
                normalizedDetails,
                now,
                messageSnapshot);
    }

    public bool StartReview(DateTimeOffset now)
    {
        if (Status == ContentReportStatus.InReview)
        {
            return false;
        }

        EnsureOpen();
        Status = ContentReportStatus.InReview;
        UpdatedAt = now;
        return true;
    }

    public void Resolve(bool dismissed, DateTimeOffset now)
    {
        if (Status is ContentReportStatus.Resolved or ContentReportStatus.Dismissed)
        {
            throw new DomainRuleException("REPORT.ALREADY_CLOSED", "The report has already been closed.");
        }

        Status = dismissed ? ContentReportStatus.Dismissed : ContentReportStatus.Resolved;
        UpdatedAt = now;
        ClosedAt = now;
    }

    private void EnsureOpen()
    {
        if (Status != ContentReportStatus.Open)
        {
            throw new DomainRuleException("REPORT.NOT_OPEN", "Only an open report can enter review.");
        }
    }
}

public sealed class ModerationCase
{
    private ModerationCase()
    {
    }

    private ModerationCase(Guid id, Guid reportId, DateTimeOffset now)
    {
        Id = id;
        ReportId = reportId;
        Status = ModerationCaseStatus.Open;
        Version = 1;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid ReportId { get; private set; }

    public ModerationCaseStatus Status { get; private set; }

    public Guid? AssignedToUserId { get; private set; }

    public long Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public static ModerationCase Create(Guid reportId, DateTimeOffset now)
    {
        if (reportId == Guid.Empty)
        {
            throw new DomainRuleException("MODERATION.REPORT_REQUIRED", "A moderation case requires a report.");
        }

        return new ModerationCase(Guid.CreateVersion7(), reportId, now);
    }

    public bool StartReview(Guid actorUserId, DateTimeOffset now)
    {
        if (Status == ModerationCaseStatus.InReview && AssignedToUserId == actorUserId)
        {
            return false;
        }

        if (Status != ModerationCaseStatus.Open)
        {
            throw new DomainRuleException("MODERATION.CASE_NOT_OPEN", "Only an open moderation case can enter review.");
        }

        Status = ModerationCaseStatus.InReview;
        AssignedToUserId = actorUserId;
        Version++;
        UpdatedAt = now;
        return true;
    }

    public void EnsureInReview()
    {
        if (Status != ModerationCaseStatus.InReview)
        {
            throw new DomainRuleException("MODERATION.CASE_NOT_IN_REVIEW", "The moderation case must be in review.");
        }
    }

    public void RecordDecision(DateTimeOffset now)
    {
        EnsureInReview();
        Version++;
        UpdatedAt = now;
    }

    public void Close(bool dismissed, DateTimeOffset now)
    {
        if (Status is ModerationCaseStatus.Resolved or ModerationCaseStatus.Dismissed)
        {
            throw new DomainRuleException("MODERATION.CASE_CLOSED", "The moderation case has already been closed.");
        }

        Status = dismissed ? ModerationCaseStatus.Dismissed : ModerationCaseStatus.Resolved;
        Version++;
        UpdatedAt = now;
        ClosedAt = now;
    }
}

public sealed class ModerationAction
{
    private ModerationAction()
    {
    }

    private ModerationAction(
        Guid id,
        Guid caseId,
        Guid actorUserId,
        ModerationActionType actionType,
        string reason,
        DateTimeOffset createdAt)
    {
        Id = id;
        CaseId = caseId;
        ActorUserId = actorUserId;
        ActionType = actionType;
        Reason = reason;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid CaseId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public ModerationActionType ActionType { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static ModerationAction Create(
        Guid caseId,
        Guid actorUserId,
        ModerationActionType actionType,
        string reason,
        DateTimeOffset now)
    {
        if (caseId == Guid.Empty || actorUserId == Guid.Empty)
        {
            throw new DomainRuleException("MODERATION.IDENTITY_REQUIRED", "Moderation action identifiers are required.");
        }

        string normalizedReason = reason.Trim();
        if (normalizedReason.Length is < 8 or > 1000)
        {
            throw new DomainRuleException(
                "MODERATION.REASON_INVALID",
                "A moderation action reason must contain between 8 and 1000 characters.");
        }

        return new ModerationAction(Guid.CreateVersion7(), caseId, actorUserId, actionType, normalizedReason, now);
    }
}
