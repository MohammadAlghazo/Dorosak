namespace Dorosak.Application.Features.Engagement;

public sealed record CourseReviewResponse(
    Guid Id,
    Guid CourseId,
    Guid UserId,
    string AuthorName,
    short Rating,
    string Text,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CourseReviewPageResponse(
    IReadOnlyList<CourseReviewResponse> Items,
    decimal AverageRating,
    int TotalCount,
    bool HasMore);

public sealed record EngagementOperationResponse(bool Completed);

public sealed record DiscussionScope(
    Guid? EnrollmentId,
    Guid? CourseId,
    Guid? ReleaseId,
    Guid? LessonId)
{
    public bool IsValid =>
        (LessonId is null || LessonId != Guid.Empty) &&
        (EnrollmentId is { } enrollmentId && enrollmentId != Guid.Empty && CourseId is null && ReleaseId is null ||
        EnrollmentId is null && CourseId is { } courseId && courseId != Guid.Empty &&
        ReleaseId is { } releaseId && releaseId != Guid.Empty);

    public static DiscussionScope ForEnrollment(Guid enrollmentId, Guid? lessonId) =>
        new(enrollmentId, null, null, lessonId);

    public static DiscussionScope ForInstructor(Guid courseId, Guid releaseId, Guid? lessonId) =>
        new(null, courseId, releaseId, lessonId);
}

public sealed record DiscussionThreadPageResponse(
    IReadOnlyList<DiscussionThreadSummaryResponse> Items,
    string? NextCursor,
    bool HasMore);

public sealed record DiscussionThreadSummaryResponse(
    Guid Id,
    Guid? LessonId,
    Guid AuthorUserId,
    string AuthorName,
    string Title,
    string Body,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsEdited,
    int CommentCount,
    bool CanEdit,
    bool CanDelete);

public sealed record DiscussionThreadResponse(
    Guid Id,
    Guid CourseId,
    Guid ReleaseId,
    Guid? LessonId,
    Guid AuthorUserId,
    string AuthorName,
    string Title,
    string Body,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsEdited,
    int CommentCount,
    bool CanEdit,
    bool CanDelete,
    DiscussionCommentPageResponse Comments);

public sealed record DiscussionCommentPageResponse(
    IReadOnlyList<DiscussionCommentResponse> Items,
    string? NextCursor,
    bool HasMore);

public sealed record DiscussionCommentResponse(
    Guid Id,
    Guid ThreadId,
    Guid? ParentCommentId,
    Guid AuthorUserId,
    string AuthorName,
    string Body,
    int Depth,
    string Status,
    bool IsEdited,
    int LikeCount,
    bool LikedByViewer,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanEdit,
    bool CanDelete);

public sealed record CommentLikeResponse(
    Guid CommentId,
    bool Liked,
    int LikeCount);
